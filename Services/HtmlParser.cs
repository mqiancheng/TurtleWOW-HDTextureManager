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
        public string Name { get; set; }  // "Less Thicc", "Performance", "Standard" 等
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
    }

    public class HtmlParser
    {
        private readonly HttpClient _httpClient;
        private static readonly string Url = "https://projectreforged.github.io/downloads/turtle/";

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

            // 找到所有分类区域 (div.sectionTitle)
            var sectionTitles = doc.DocumentNode.SelectNodes("//div[@class='sectionTitle']");
            if (sectionTitles == null) return modules;

            foreach (var sectionTitle in sectionTitles)
            {
                var categoryText = sectionTitle.InnerText.Trim();

                // 确定分类
                string category;
                if (categoryText.Contains("Core Modules"))
                    category = "Core";
                else if (categoryText.Contains("Ultra Tier"))
                    category = "Ultra";
                else if (categoryText.Contains("Optional Enhancements"))
                    category = "Optional";
                else if (categoryText.Contains("Audio"))
                    category = "Audio";  // Audio 作为独立分类
                else
                    continue;  // 跳过 Dependencies 等其他部分

                // 找到该分类下的 grid 容器（通常是 sectionTitle 的下一个兄弟节点或后面的节点）
                var grid = FindNextGrid(sectionTitle);
                if (grid == null) continue;

                // 在 grid 中查找所有直接子级的 card（section.card）
                // 只获取父级是当前 grid 的 cards，避免嵌套 grid 的干扰
                var allCards = grid.SelectNodes(".//section[@class='card']");
                if (allCards == null) continue;
                
                // 过滤出直接子级
                var directCards = new System.Collections.Generic.List<HtmlNode>();
                foreach (var card in allCards)
                {
                    if (card.ParentNode == grid)
                        directCards.Add(card);
                }
                
                if (directCards.Count == 0) continue;
                
                // 使用过滤后的列表
                var cards = directCards;

                foreach (var card in cards)
                {
                    // 提取 PATCH ID 和名称
                    var patchNameNode = card.SelectSingleNode(".//h2[@class='patchName']");
                    if (patchNameNode == null) continue;

                    var text = patchNameNode.InnerText.Trim();
                    var match = Regex.Match(text, @"PATCH-([A-Z])", RegexOptions.IgnoreCase);
                    if (!match.Success) continue;

                    var patchId = $"PATCH-{match.Groups[1].Value.ToUpperInvariant()}";

                    // 提取名称（去掉 PATCH-X 前缀和表情符号）
                    var name = ExtractPatchName(text, patchId);

                    // 提取描述
                    var descNode = card.SelectSingleNode(".//p[@class='desc']");
                    var description = descNode?.InnerText.Trim() ?? string.Empty;

                    // 依赖关系在 ParseModulesAsync 最后统一设置，不从网页解析

                    // 检查是否有变体版本（subchoices）
                    var subchoices = card.SelectSingleNode(".//div[@class='subchoices']");
                    if (subchoices != null)
                    {
                        // 有变体版本（如 PATCH-L, PATCH-U）
                        var variants = ExtractVariants(subchoices, patchId);
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
                                Dependencies = new List<string>() // 依赖关系在后面统一设置
                            });
                        }
                    }
                    else
                    {
                        // 普通 PATCH（没有变体）
                        var version = ExtractVersionFromCard(card);
                        var downloadUrl = ExtractDownloadUrlFromCard(card);

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
                                Dependencies = new List<string>() // 依赖关系在后面统一设置
                            });
                        }
                    }
                }
            }

            // 分类排序: Core -> Optional -> Audio -> Ultra (Ultra 移到最后)
            var sortedModules = modules.OrderBy(m => m.Category == "Core" ? 0 : 
                                        m.Category == "Optional" ? 1 : 
                                        m.Category == "Audio" ? 2 : 3)
                          .ThenBy(m => m.Id)
                          .ToList();

            // 手动修正依赖关系（网页解析可能不准确）
            ApplyCorrectDependencies(sortedModules);

            return sortedModules;
        }

        private HtmlNode FindNextGrid(HtmlNode sectionTitle)
        {
            // 查找 sectionTitle 后面的 grid div
            var current = sectionTitle.NextSibling;
            int siblingCount = 0;
            while (current != null)
            {
                siblingCount++;
                if (current.NodeType == HtmlNodeType.Element)
                {
                    var className = current.GetAttributeValue("class", "");
                    if (current.Name == "div" && className == "grid")
                    {
                        return current;
                    }
                    // 如果遇到下一个 sectionTitle，说明当前分类没有 grid
                    if (className == "sectionTitle")
                    {
                        return null;
                    }
                }
                current = current.NextSibling;
            }
            return null;
        }

        private string ExtractPatchName(string text, string patchId)
        {
            // 去掉 PATCH-X 部分
            var name = Regex.Replace(text, @"PATCH-[A-Z]\s*", "", RegexOptions.IgnoreCase).Trim();
            
            // 去掉开头的表情符号（Unicode ranges for emojis）
            name = Regex.Replace(name, @"^[\u2600-\u26FF\u2700-\u27BF\u2B50\u2934-\u2935\u2B06\u2194-\u2199\u3030\uFE0F\s]+", "").Trim();
            
            // 去掉分隔符（如 —）
            name = Regex.Replace(name, @"^[—\-\s]+", "").Trim();
            
            if (string.IsNullOrEmpty(name)) 
                name = patchId;
            
            return name;
        }

        private string ExtractVersionFromCard(HtmlNode card)
        {
            // 从 updateTag 中提取版本号
            var updateTag = card.SelectSingleNode(".//span[@class='updateTag']");
            if (updateTag != null)
            {
                var text = updateTag.InnerText;
                var match = Regex.Match(text, @"v(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Value.ToLowerInvariant();
            }
            return "unknown";
        }

        private string ExtractDownloadUrlFromCard(HtmlNode card)
        {
            // 查找下载按钮（btn primary）
            var link = card.SelectSingleNode(".//a[contains(@class, 'btn') and contains(@class, 'primary')]");
            if (link != null)
            {
                var href = link.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    // 处理相对路径
                    if (href.StartsWith("http"))
                        return href;
                    if (href.StartsWith("/"))
                        return $"https://projectreforged.github.io{href}";
                }
            }
            return null;
        }

        private List<PatchVariant> ExtractVariants(HtmlNode subchoices, string patchId)
        {
            var variants = new List<PatchVariant>();

            // 只查找直接子级的 choice div，避免抓取到其他 section 的内容
            // 使用显式的子节点遍历而不是 XPath，确保只获取直接子级
            var choices = new List<HtmlNode>();
            foreach (var child in subchoices.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Element && 
                    child.Name == "div" && 
                    child.GetAttributeValue("class", "") == "choice")
                {
                    choices.Add(child);
                }
            }

            if (choices.Count == 0) return variants;

            foreach (var choice in choices)
            {
                // 提取变体名称 - 只查找直接子 div 下的 b 标签
                var nameNode = choice.SelectSingleNode(".//b");
                if (nameNode == null) continue;

                var variantName = nameNode.InnerText.Trim();
                
                // 跳过非补丁下载的按钮（如 VanillaHelpers, DXVK）
                var link = choice.SelectSingleNode(".//a[contains(@class, 'btn') and contains(@class, 'primary')]");
                if (link == null) continue;
                
                var href = link.GetAttributeValue("href", "");
                // 只处理 MPQ 文件链接和 R2.dev 链接
                if (!href.EndsWith(".mpq", StringComparison.OrdinalIgnoreCase) && 
                    !href.Contains("r2.dev/patches"))
                    continue;
                
                // 标准化变体名称（同时处理拼写错误的 "Thicc" 和正确的 "Thick"）
                if (variantName.IndexOf("Thicc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    variantName.IndexOf("Thick", StringComparison.OrdinalIgnoreCase) >= 0)
                    variantName = "Less Thick";
                else if (variantName.IndexOf("Performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         variantName.Equals("Perf", StringComparison.OrdinalIgnoreCase))
                    variantName = "Performance";
                else if (variantName.IndexOf("Regular", StringComparison.OrdinalIgnoreCase) >= 0 || 
                         variantName.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0)
                    variantName = "Standard";
                else
                {
                    // 无法识别的变体名称，跳过
                    continue;
                }
                
                // 检查是否已经添加过相同名称的变体（去重）
                if (variants.Any(v => v.Name == variantName))
                {
                    continue;
                }
                
                // 处理相对路径
                if (!href.StartsWith("http"))
                    href = href.StartsWith("/") ? $"https://projectreforged.github.io{href}" : null;

                if (href == null) continue;

                // 提取版本（从 updateTag 中）
                var updateTag = choice.SelectSingleNode(".//span[@class='updateTag']");
                var version = "unknown";
                if (updateTag != null)
                {
                    var match = Regex.Match(updateTag.InnerText, @"v(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                    if (match.Success)
                        version = match.Value.ToLowerInvariant();
                }

                // 如果没有在 choice 中找到版本，尝试从父级 card 中找
                if (version == "unknown")
                {
                    var parentCard = subchoices.ParentNode;
                    if (parentCard != null)
                    {
                        var cardUpdateTag = parentCard.SelectSingleNode(".//span[@class='updateTag']");
                        if (cardUpdateTag != null)
                        {
                            var match = Regex.Match(cardUpdateTag.InnerText, @"v(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                            if (match.Success)
                                version = match.Value.ToLowerInvariant();
                        }
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
                        // U 依赖 A + G
                        module.Dependencies.Add("PATCH-A");
                        module.Dependencies.Add("PATCH-G");
                        break;
                }
                
                // 移除自身的依赖（如果有）
                module.Dependencies.RemoveAll(d => d == module.Id);
                // 去重
                module.Dependencies = module.Dependencies.Distinct().ToList();
            }
        }
    }
}
