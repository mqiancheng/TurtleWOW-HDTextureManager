using HtmlAgilityPack;
using HDTextureManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HDTextureManager.Services
{
    public class PatchVariant
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
    }

    public class HtmlParser
    {
        private readonly HttpClient _httpClient;
        private static readonly string Url = "https://projectreforged.github.io/vanilla/downloads/turtle/";

        public HtmlParser(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<PatchModule>> ParseModulesAsync()
        {
            var html = await _httpClient.GetStringAsync(Url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var modules = new List<PatchModule>();

            // 找到所有分类区域 (div.dl-group)
            var groups = doc.DocumentNode.SelectNodes("//div[@class='dl-group']");
            if (groups == null) return modules;

            foreach (var group in groups)
            {
                // 提取分类名称 (group-header > group-label)
                var labelNode = group.SelectSingleNode(".//span[@class='group-label']");
                if (labelNode == null) continue;

                var categoryText = labelNode.InnerText.Trim();

                // 确定分类
                string category;
                if (categoryText.Contains("Dependencies"))
                    continue;  // 跳过 Dependencies 区域（VanillaHelpers、DXVK）
                else if (categoryText.Contains("Core"))
                    category = "Core";
                else if (categoryText.Contains("Optional"))
                    category = "Optional";
                else if (categoryText.Equals("Audio", StringComparison.OrdinalIgnoreCase))
                    category = "Audio";
                else if (categoryText.Contains("HD Tier") || categoryText.Contains("Ultra"))
                    category = "Ultra";
                else
                    continue;

                // 在当前 dl-group 中查找所有 dl-card
                var cards = group.SelectNodes(".//div[@class='dl-card']");
                if (cards == null) continue;

                foreach (var card in cards)
                {
                    // 提取 PATCH ID (dl-patch)
                    var patchIdNode = card.SelectSingleNode(".//span[@class='dl-patch']");
                    if (patchIdNode == null) continue;

                    var patchIdText = patchIdNode.InnerText.Trim();
                    var match = Regex.Match(patchIdText, @"PATCH-([A-Z])", RegexOptions.IgnoreCase);
                    if (!match.Success) continue;

                    var patchId = $"PATCH-{match.Groups[1].Value.ToUpperInvariant()}";

                    // 提取名称 (dl-name)
                    var nameNode = card.SelectSingleNode(".//span[@class='dl-name']");
                    var name = nameNode?.InnerText.Trim() ?? patchId;

                    // 提取描述 (dl-desc)
                    var descNode = card.SelectSingleNode(".//div[@class='dl-desc']");
                    var description = descNode?.InnerText.Trim() ?? string.Empty;

                    // 检查是否有变体版本（dl-variants）
                    var variantsContainer = card.SelectSingleNode(".//div[@class='dl-variants']");
                    if (variantsContainer != null)
                    {
                        // 有变体版本（如 PATCH-L, PATCH-T）
                        var variants = ExtractVariantsNew(variantsContainer, patchId);
                        foreach (var variant in variants)
                        {
                            modules.Add(new PatchModule
                            {
                                Id = patchId,
                                Name = name,
                                Variant = variant.Name,
                                Version = variant.Version,
                                DownloadUrl = variant.DownloadUrl,
                                Category = category,
                                Description = description,
                                Dependencies = new List<string>()
                            });
                        }
                    }
                    else
                    {
                        // 普通 PATCH（没有变体）
                        var version = ExtractVersionFromCardNew(card);
                        var downloadUrl = ExtractDownloadUrlFromCardNew(card);

                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            modules.Add(new PatchModule
                            {
                                Id = patchId,
                                Name = name,
                                Version = version,
                                DownloadUrl = downloadUrl,
                                Category = category,
                                Description = description,
                                Dependencies = new List<string>()
                            });
                        }
                    }
                }
            }

            // 分类排序: Core -> Optional -> Audio -> Ultra
            var sortedModules = modules.OrderBy(m => m.Category == "Core" ? 0 :
                                        m.Category == "Optional" ? 1 :
                                        m.Category == "Audio" ? 2 : 3)
                          .ThenBy(m => m.Id)
                          .ToList();

            // 手动修正依赖关系
            ApplyCorrectDependencies(sortedModules);

            return sortedModules;
        }

        private string ExtractVersionFromCardNew(HtmlNode card)
        {
            // 从 dl-version 中提取版本号，格式如 "Updated · v5.5.1" 或 "v5.0.0"
            var versionNode = card.SelectSingleNode(".//div[@class='dl-version']");
            if (versionNode != null)
            {
                var text = versionNode.InnerText;
                var match = Regex.Match(text, @"v(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Value.ToLowerInvariant();
            }
            return "unknown";
        }

        private string ExtractDownloadUrlFromCardNew(HtmlNode card)
        {
            // 查找主下载按钮 (btn-dl btn-primary)，在 dl-actions 内或直接在 card 内
            var link = card.SelectSingleNode(".//a[contains(@class,'btn-dl') and contains(@class,'btn-primary')]");
            if (link != null)
            {
                var href = link.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    if (href.StartsWith("http"))
                        return href;
                    if (href.StartsWith("/"))
                        return $"https://projectreforged.github.io{href}";
                }
            }
            return null;
        }

        private List<PatchVariant> ExtractVariantsNew(HtmlNode variantsContainer, string patchId)
        {
            var variants = new List<PatchVariant>();

            // 遍历直接子级的 dl-variant
            foreach (var child in variantsContainer.ChildNodes)
            {
                if (child.NodeType != HtmlNodeType.Element ||
                    child.Name != "div" ||
                    child.GetAttributeValue("class", "") != "dl-variant")
                    continue;

                // 提取变体名称 (dl-variant-name)
                var nameNode = child.SelectSingleNode(".//div[@class='dl-variant-name']");
                if (nameNode == null) continue;

                var rawName = nameNode.InnerText.Trim();

                // 标准化变体名称
                string variantName;
                if (rawName.IndexOf("Regular", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rawName.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0)
                    variantName = "Standard";
                else if (rawName.IndexOf("Thicc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         rawName.IndexOf("Thick", StringComparison.OrdinalIgnoreCase) >= 0)
                    variantName = "Less Thick";
                else if (rawName.IndexOf("Performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         rawName.Equals("Perf", StringComparison.OrdinalIgnoreCase))
                    variantName = "Performance";
                else if (rawName.IndexOf("Ultra Base", StringComparison.OrdinalIgnoreCase) >= 0)
                    variantName = "Ultra Base";
                else
                    continue;  // 无法识别的变体名称

                // 跳过非补丁下载的链接（如 VanillaHelpers, DXVK），只处理 .mpq 和 r2.dev/patches
                var link = child.SelectSingleNode(".//a[contains(@class,'btn-dl')]");
                if (link == null) continue;

                var href = link.GetAttributeValue("href", "");
                if (!href.EndsWith(".mpq", StringComparison.OrdinalIgnoreCase) &&
                    !href.Contains("r2.dev/patches"))
                    continue;

                // 处理相对路径
                if (!href.StartsWith("http"))
                    href = href.StartsWith("/") ? $"https://projectreforged.github.io{href}" : null;

                if (string.IsNullOrEmpty(href)) continue;

                // 去重
                if (variants.Any(v => v.Name == variantName))
                    continue;

                // 提取版本：先从父级卡片找，变体本身可能没有版本号
                string version = "unknown";
                var parentCard = variantsContainer.ParentNode;
                if (parentCard != null)
                {
                    var cardVersionNode = parentCard.SelectSingleNode(".//div[@class='dl-version']");
                    if (cardVersionNode != null)
                    {
                        var verMatch = Regex.Match(cardVersionNode.InnerText, @"v(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                        if (verMatch.Success)
                            version = verMatch.Value.ToLowerInvariant();
                    }
                }

                variants.Add(new PatchVariant
                {
                    Name = variantName,
                    Version = version,
                    DownloadUrl = href
                });
            }

            return variants;
        }

        /// <summary>
        /// 应用正确的依赖关系（硬编码，不从网页解析）
        /// </summary>
        private void ApplyCorrectDependencies(List<PatchModule> modules)
        {
            foreach (var module in modules)
            {
                module.Dependencies.Clear();

                switch (module.Id)
                {
                    case "PATCH-O":
                        // O 依赖 C + S
                        module.Dependencies.Add("PATCH-C");
                        module.Dependencies.Add("PATCH-S");
                        break;
                    case "PATCH-T":
                        // T 依赖 A + G
                        module.Dependencies.Add("PATCH-A");
                        module.Dependencies.Add("PATCH-G");
                        break;
                    case "PATCH-B":
                    case "PATCH-D":
                    case "PATCH-E":
                        // B、D、E 互相依赖
                        module.Dependencies.Add("PATCH-B");
                        module.Dependencies.Add("PATCH-D");
                        module.Dependencies.Add("PATCH-E");
                        break;
                    case "PATCH-L":
                        // L 依赖 A
                        module.Dependencies.Add("PATCH-A");
                        break;
                    case "PATCH-U":
                        // U 依赖 A + G + T (Ultra Base)
                        module.Dependencies.Add("PATCH-A");
                        module.Dependencies.Add("PATCH-G");
                        module.Dependencies.Add("PATCH-T");
                        break;
                }

                // 移除自身的依赖
                module.Dependencies.RemoveAll(d => d == module.Id);
                // 去重
                module.Dependencies = module.Dependencies.Distinct().ToList();
            }
        }
    }
}
