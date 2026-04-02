using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Windows;
using System.Windows.Data;

namespace HDTextureManager.Services
{
    /// <summary>
    /// 多语言本地化服务 - 使用硬编码字典，单文件部署友好
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        private readonly Dictionary<string, Dictionary<string, string>> _translations;
        private CultureInfo _currentCulture;

        public event PropertyChangedEventHandler PropertyChanged;

        private LocalizationService()
        {
            _translations = InitializeTranslations();
            _currentCulture = GetDefaultCulture();
        }

        /// <summary>
        /// 初始化所有语言翻译（硬编码，确保单文件部署正常工作）
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> InitializeTranslations()
        {
            var translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            // 简体中文 (zh-CN) - 默认语言
            translations["zh-CN"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 标题和通用
                ["AppTitle"] = "Project Reforged 管理器",
                
                // 设置面板
                ["Settings_Title"] = "设置",
                ["Settings_GamePath"] = "选择 WoW.exe",
                ["Settings_Browse"] = "浏览...",
                ["Settings_InstallPath"] = "安装路径",
                ["Settings_Language"] = "语言",
                ["Settings_ClearCache"] = "清除缓存",
                ["Settings_Author"] = "作者：海蓝钢板",
                ["Settings_4GBPatch"] = "4GB 补丁",

                // 分类标题
                ["Section_Core"] = "核心模块",
                ["Section_Optional"] = "可选增强",
                ["Section_Audio"] = "音频增强",
                ["Section_Ultra"] = "超高清材质",
                
                // 状态
                ["Status_Ready"] = "就绪",
                ["Status_Loading"] = "正在加载 {0} 个模块...",
                ["Status_Loaded"] = "已加载 {0} 个模块",
                ["Status_LoadedLocal"] = "，本地存在 {0} 个补丁文件",
                ["Status_LoadedUnknown"] = " (发现 {0} 个未知文件)",
                ["Status_LoadFailed"] = "加载失败: {0}",
                ["Status_Downloading"] = "正在下载 {0}... {1}%",
                ["Status_DownloadFailed"] = "下载失败: {0}",
                ["Status_Scanning"] = "正在扫描...",
                ["Status_SetGameDir"] = "请先设置游戏目录",
                ["Status_GameStarted"] = "游戏已启动",
                ["Status_WebOpened"] = "网页已打开",
                ["Status_ModuleEnabled"] = "{0} 已启用",
                ["Status_ModuleDisabled"] = "{0} 已停用",
                ["Status_CacheCleared"] = "已清理 {0} 个缓存文件 ({1})",
                ["Status_RefreshFailed"] = "刷新失败: {0}",
                
                // 按钮动作
                ["Action_Download"] = "下载安装",
                ["Action_Downloading"] = "下载中...",
                ["Action_Paused"] = "暂停",
                ["Action_Resume"] = "继续下载 ({0})",
                ["Action_Cancel"] = "取消",
                ["Action_Enable"] = "启用",
                ["Action_Disable"] = "停用",
                ["Action_Disabled"] = "已停用",
                ["Action_Update"] = "更新到 {0}",
                ["Action_Latest"] = "已是最新",
                
                // 模块信息
                ["Module_NoDescription"] = "暂无描述",
                ["Module_Dependencies"] = "依赖: {0}",
                ["Module_LocalVersion"] = "本地: {0}",
                ["Module_LatestVersionUnknown"] = "最新: 获取中...",
                ["Module_LatestVersion"] = "最新: {0}",
                ["Module_VersionUnknown"] = "未知",
                ["Module_VersionTooltip"] = "版本未知，请点击下载按钮更新",
                ["Module_SizeMB"] = "{0:F1} MB",
                
                // 消息标题
                ["Msg_WelcomeTitle"] = "欢迎使用",
                ["Msg_ErrorTitle"] = "错误",
                ["Msg_InfoTitle"] = "提示",
                ["Msg_ConfirmTitle"] = "请确认",
                
                // 消息内容
                ["Msg_Welcome"] = "欢迎使用 Project Reforged 管理器！\n\n首次使用请设置游戏目录。",
                ["Msg_SetGameDir"] = "请先设置游戏目录",
                ["Msg_GameNotFound"] = "未找到 WoW.exe，请确认游戏目录是否正确",
                ["Msg_SelectWowExe"] = "请选择 WoW.exe 文件",
                ["Msg_DataDirNotExist"] = "游戏目录不存在，请先设置正确的游戏目录",
                ["Msg_NoTempFiles"] = "没有需要清理的临时文件",
                ["Msg_ClearFailed"] = "清理失败：{0}",
                ["Msg_LoadError"] = "加载数据失败：{0}",
                ["Msg_DownloadFailed"] = "下载失败：{0}",
                ["Msg_FileNotFound"] = "文件不存在：{0}",
                ["Msg_OperationFailed"] = "操作失败：{0}",
                ["Msg_LaunchFailed"] = "启动失败：{0}",
                ["Msg_OpenWebFailed"] = "打开网页失败：{0}",
                ["Msg_ConfirmClearCache"] = "发现 {0} 个未完成的下载文件，是否清理？\n\n总大小: {1:F1} MB",
                ["Msg_CacheCleared"] = "成功清理 {0} 个文件\n释放空间: {1}",
                ["Msg_MissingDeps"] = "缺少以下依赖补丁: {0}\n\n请先下载",
                ["Msg_CannotEnable"] = "无法启用 {0}\n\n缺少以下依赖补丁:\n{1}\n\n请先下载并安装这些依赖补丁。",
                ["Msg_StopDownload"] = "当前有正在进行的下载任务。\n停止下载并退出吗？",
                ["Msg_StopDownloadTitle"] = "确认退出",
                
                // 系统依赖
                ["Msg_MissingSystemDeps"] = "检测到缺少以下系统依赖文件:\n{0}\n\n请启动官方启动器，在「模组」页面安装 VanillaHelpers 和 DXVK。",
                ["Msg_DisabledSystemDeps"] = "检测到以下系统依赖未启用:\n{0}\n\n请启动官方启动器，在「模组」页面启用 VanillaHelpers 和 DXVK。",
                ["Status_MissingSystemDeps"] = "缺少系统依赖，请在官方启动器中安装",
                ["Status_DisabledSystemDeps"] = "系统依赖未启用，请在官方启动器中启用",
                
                // 下载状态
                ["Dl_DownloadingFiles"] = "正在下载 {0} 个文件 ({1} 进行中, {2} 已暂停), 平均进度: {3:F0}%",
                ["Dl_SingleDownloading"] = "正在下载: {0}, 进度: {1:F0}%",
                ["Dl_SinglePaused"] = "已暂停: {0}",

                // 文件大小单位
                ["Size_MB"] = "{0:F1} MB",
                ["Size_KB"] = "{0:F1} KB",
                ["Size_Bytes"] = "{0} 字节",

                // GPU 提示（本地化新增）
                ["Msg_AmdOnly"] = "检测到 AMD（A 卡）。请使用 DXVK 2.6 及以下版本，进入游戏前切换到系统英文输入法并保持英文状态。",
                ["Msg_AmdNvidiaMixed"] = "检测到独立 NVIDIA 与集成 AMD。请用独立 NVIDIA（N 卡）启动游戏以避免兼容问题。"
            };

            // 英语 (en)
            translations["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "Project Reforged Manager",
                ["Settings_Title"] = "Settings",
                ["Settings_GamePath"] = "Select WoW.exe",
                ["Settings_Browse"] = "Browse...",
                ["Settings_InstallPath"] = "Install Path",
                ["Settings_Language"] = "Language",
                ["Settings_ClearCache"] = "Clear Cache",
                ["Settings_Author"] = "Author: Cliencer",
                ["Settings_4GBPatch"] = "4GB Patch",
                ["Section_Core"] = "Core Modules",
                ["Section_Optional"] = "Optional Enhancements",
                ["Section_Audio"] = "Audio Enhancements",
                ["Section_Ultra"] = "Ultra HD Textures",
                ["Status_Ready"] = "Ready",
                ["Status_Loading"] = "Loading {0} modules...",
                ["Status_Loaded"] = "Loaded {0} modules",
                ["Status_LoadedLocal"] = ", {0} local patches found",
                ["Status_LoadedUnknown"] = " ({0} unknown files found)",
                ["Status_LoadFailed"] = "Load failed: {0}",
                ["Status_Downloading"] = "Downloading {0}... {1}%",
                ["Status_DownloadFailed"] = "Download failed: {0}",
                ["Status_Scanning"] = "Scanning...",
                ["Status_SetGameDir"] = "Please set game directory first",
                ["Status_GameStarted"] = "Game started",
                ["Status_WebOpened"] = "Web page opened",
                ["Status_ModuleEnabled"] = "{0} enabled",
                ["Status_ModuleDisabled"] = "{0} disabled",
                ["Status_CacheCleared"] = "Cleared {0} cache files ({1})",
                ["Status_RefreshFailed"] = "Refresh failed: {0}",
                ["Action_Download"] = "Download & Install",
                ["Action_Downloading"] = "Downloading...",
                ["Action_Paused"] = "Paused",
                ["Action_Resume"] = "Resume ({0})",
                ["Action_Cancel"] = "Cancel",
                ["Action_Enable"] = "Enable",
                ["Action_Disable"] = "Disable",
                ["Action_Disabled"] = "Disabled",
                ["Action_Update"] = "Update to {0}",
                ["Action_Latest"] = "Latest",
                ["Module_NoDescription"] = "No description available",
                ["Module_Dependencies"] = "Dependencies: {0}",
                ["Module_LocalVersion"] = "Local: {0}",
                ["Module_LatestVersionUnknown"] = "Latest: loading...",
                ["Module_LatestVersion"] = "Latest: {0}",
                ["Module_VersionUnknown"] = "Unknown",
                ["Module_VersionTooltip"] = "Version unknown, click download to update",
                ["Module_SizeMB"] = "{0:F1} MB",
                ["Msg_WelcomeTitle"] = "Welcome",
                ["Msg_ErrorTitle"] = "Error",
                ["Msg_InfoTitle"] = "Information",
                ["Msg_ConfirmTitle"] = "Confirm",
                ["Msg_Welcome"] = "Welcome to Project Reforged Manager!\n\nPlease set the game directory first.",
                ["Msg_SetGameDir"] = "Please set the game directory first",
                ["Msg_GameNotFound"] = "WoW.exe not found, please check the game directory",
                ["Msg_SelectWowExe"] = "Please select WoW.exe",
                ["Msg_DataDirNotExist"] = "Game directory does not exist, please set the correct path",
                ["Msg_NoTempFiles"] = "No temporary files to clean",
                ["Msg_ClearFailed"] = "Clear failed: {0}",
                ["Msg_LoadError"] = "Failed to load data: {0}",
                ["Msg_DownloadFailed"] = "Download failed: {0}",
                ["Msg_FileNotFound"] = "File not found: {0}",
                ["Msg_OperationFailed"] = "Operation failed: {0}",
                ["Msg_LaunchFailed"] = "Launch failed: {0}",
                ["Msg_OpenWebFailed"] = "Open web page failed: {0}",
                ["Msg_ConfirmClearCache"] = "Found {0} incomplete download files, clear them?\n\nTotal size: {1:F1} MB",
                ["Msg_CacheCleared"] = "Successfully cleared {0} files\nFreed space: {1}",
                ["Msg_MissingDeps"] = "Missing dependencies: {0}\n\nPlease download them first",
                ["Msg_CannotEnable"] = "Cannot enable {0}\n\nMissing dependencies:\n{1}\n\nPlease download and install these dependencies first.",
                ["Msg_StopDownload"] = "There are active downloads.\nStop downloading and exit?",
                ["Msg_StopDownloadTitle"] = "Confirm Exit",
                
                // System Dependencies
                ["Msg_MissingSystemDeps"] = "Missing system dependency files:\n{0}\n\nPlease launch the official launcher and install VanillaHelpers and DXVK from the Mods page.",
                ["Msg_DisabledSystemDeps"] = "The following system dependencies are disabled:\n{0}\n\nPlease launch the official launcher and enable VanillaHelpers and DXVK from the Mods page.",
                ["Status_MissingSystemDeps"] = "Missing system dependencies, please install in official launcher",
                ["Status_DisabledSystemDeps"] = "System dependencies disabled, please enable in official launcher",
                
                // Download Status
                ["Dl_DownloadingFiles"] = "Downloading {0} files ({1} active, {2} paused), avg progress: {3:F0}%",
                ["Dl_SingleDownloading"] = "Downloading: {0}, progress: {1:F0}%",
                ["Dl_SinglePaused"] = "Paused: {0}",

                // GPU messages
                ["Msg_AmdOnly"] = "Detected AMD GPU. Use DXVK 2.6 or earlier. Before launch, switch to the system English input method and keep it active.",
                ["Msg_AmdNvidiaMixed"] = "Detected discrete NVIDIA and integrated AMD GPUs. Please launch the game with the discrete NVIDIA GPU to avoid compatibility issues."
            };

            // 西班牙语 (es)
            translations["es"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "Administrador de Project Reforged",
                ["Settings_Title"] = "Configuración",
                ["Settings_GamePath"] = "Seleccionar WoW.exe",
                ["Settings_Browse"] = "Examinar...",
                ["Settings_InstallPath"] = "Ruta de instalación",
                ["Settings_Language"] = "Idioma",
                ["Settings_ClearCache"] = "Borrar caché",
                ["Settings_Author"] = "Autor: Cliencer",
                ["Settings_4GBPatch"] = "Parche 4GB",
                ["Section_Core"] = "Módulos principales",
                ["Section_Optional"] = "Mejoras opcionales",
                ["Section_Audio"] = "Mejoras de audio",
                ["Section_Ultra"] = "Texturas Ultra HD",
                ["Status_Ready"] = "Listo",
                ["Status_Loading"] = "Cargando {0} módulos...",
                ["Status_Loaded"] = "Cargados {0} módulos",
                ["Status_LoadedLocal"] = ", {0} parches locales encontrados",
                ["Status_LoadedUnknown"] = " ({0} archivos desconocidos)",
                ["Status_LoadFailed"] = "Error al cargar: {0}",
                ["Status_Downloading"] = "Descargando {0}... {1}%",
                ["Status_DownloadFailed"] = "Error de descarga: {0}",
                ["Status_Scanning"] = "Escaneando...",
                ["Status_SetGameDir"] = "Por favor configure el directorio del juego",
                ["Status_GameStarted"] = "Juego iniciado",
                ["Status_WebOpened"] = "Página web abierta",
                ["Status_ModuleEnabled"] = "{0} activado",
                ["Status_ModuleDisabled"] = "{0} desactivado",
                ["Status_CacheCleared"] = "Limpiados {0} archivos de caché ({1})",
                ["Status_RefreshFailed"] = "Error al actualizar: {0}",
                ["Action_Download"] = "Descargar e instalar",
                ["Action_Downloading"] = "Descargando...",
                ["Action_Paused"] = "Pausado",
                ["Action_Resume"] = "Continuar ({0})",
                ["Action_Cancel"] = "Cancelar",
                ["Action_Enable"] = "Activar",
                ["Action_Disable"] = "Desactivar",
                ["Action_Disabled"] = "Desactivado",
                ["Action_Update"] = "Actualizar a {0}",
                ["Action_Latest"] = "Última versión",
                ["Module_NoDescription"] = "Sin descripción",
                ["Module_Dependencies"] = "Dependencias: {0}",
                ["Module_LocalVersion"] = "Local: {0}",
                ["Module_LatestVersionUnknown"] = "Última: cargando...",
                ["Module_LatestVersion"] = "Última: {0}",
                ["Module_VersionUnknown"] = "Desconocida",
                ["Module_VersionTooltip"] = "Versión desconocida, haga clic en descargar para actualizar",
                ["Module_SizeMB"] = "{0:F1} MB",
                ["Msg_WelcomeTitle"] = "Bienvenido",
                ["Msg_ErrorTitle"] = "Error",
                ["Msg_InfoTitle"] = "Información",
                ["Msg_ConfirmTitle"] = "Confirmar",
                ["Msg_Welcome"] = "¡Bienvenido al Administrador de Project Reforged!\n\nPor favor configure el directorio del juego.",
                ["Msg_SetGameDir"] = "Por favor configure el directorio del juego",
                ["Msg_GameNotFound"] = "WoW.exe no encontrado, verifique el directorio",
                ["Msg_SelectWowExe"] = "Seleccione WoW.exe",
                ["Msg_DataDirNotExist"] = "El directorio no existe, configure la ruta correcta",
                ["Msg_NoTempFiles"] = "No hay archivos temporales para limpiar",
                ["Msg_ClearFailed"] = "Error al limpiar: {0}",
                ["Msg_LoadError"] = "Error al cargar datos: {0}",
                ["Msg_DownloadFailed"] = "Error de descarga: {0}",
                ["Msg_FileNotFound"] = "Archivo no encontrado: {0}",
                ["Msg_OperationFailed"] = "Operación fallida: {0}",
                ["Msg_LaunchFailed"] = "Error al iniciar: {0}",
                ["Msg_OpenWebFailed"] = "Error al abrir página web: {0}",
                ["Msg_ConfirmClearCache"] = "Encontrados {0} archivos de descarga incompletos, ¿limpiar?\n\nTamaño total: {1:F1} MB",
                ["Msg_CacheCleared"] = "Limpiados {0} archivos\nEspacio liberado: {1}",
                ["Msg_MissingDeps"] = "Faltan dependencias: {0}\n\nDescárguelas primero",
                ["Msg_CannotEnable"] = "No se puede activar {0}\n\nFaltan dependencias:\n{1}\n\nDescárguelas e instálelas primero.",
                ["Msg_StopDownload"] = "Hay descargas activas.\n¿Detener descargas и salir?",
                ["Msg_StopDownloadTitle"] = "Confirmar Salida",
                
                // Dependencias del Sistema
                ["Msg_MissingSystemDeps"] = "Faltan archivos de dependencia del sistema:\n{0}\n\nInicie el lanzador oficial e instale VanillaHelpers y DXVK desde la página de Mods.",
                ["Msg_DisabledSystemDeps"] = "Las siguientes dependencias del sistema están deshabilitadas:\n{0}\n\nInicie el lanzador oficial y habilite VanillaHelpers y DXVK desde la página de Mods.",
                ["Status_MissingSystemDeps"] = "Faltan dependencias del sistema, instálelas en el lanzador oficial",
                ["Status_DisabledSystemDeps"] = "Dependencias del sistema deshabilitadas, habilítelas en el lanzador oficial",
                
                // Download Status
                ["Dl_DownloadingFiles"] = "Descargando {0} archivos ({1} activos, {2} pausados), progreso: {3:F0}%",
                ["Dl_SingleDownloading"] = "Descargando: {0}, progreso: {1:F0}%",
                ["Dl_SinglePaused"] = "Pausado: {0}",

                // GPU messages
                ["Msg_AmdOnly"] = "Se detectó GPU AMD. Use DXVK 2.6 o anterior. Antes de iniciar, cambie al método de entrada en inglés del sistema y manténgalo activo.",
                ["Msg_AmdNvidiaMixed"] = "Se detectaron GPUs NVIDIA (discreta) y AMD (integrada). Inicie el juego con la GPU NVIDIA discreta para evitar problemas de compatibilidad."
            };

            // 德语 (de)
            translations["de"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "Project Reforged Manager",
                ["Settings_Title"] = "Einstellungen",
                ["Settings_GamePath"] = "WoW.exe auswählen",
                ["Settings_Browse"] = "Durchsuchen...",
                ["Settings_InstallPath"] = "Installationspfad",
                ["Settings_Language"] = "Sprache",
                ["Settings_ClearCache"] = "Cache löschen",
                ["Settings_Author"] = "Autor: Cliencer",
                ["Settings_4GBPatch"] = "4GB-Patch",
                ["Section_Core"] = "Kernmodule",
                ["Section_Optional"] = "Optionale Verbesserungen",
                ["Section_Audio"] = "Audio-Verbesserungen",
                ["Section_Ultra"] = "Ultra HD Texturen",
                ["Status_Ready"] = "Bereit",
                ["Status_Loading"] = "Lade {0} Module...",
                ["Status_Loaded"] = "{0} Module geladen",
                ["Status_LoadedLocal"] = ", {0} lokale Patches gefunden",
                ["Status_LoadedUnknown"] = " ({0} unbekannte Dateien)",
                ["Status_LoadFailed"] = "Laden fehlgeschlagen: {0}",
                ["Status_Downloading"] = "Lade {0} herunter... {1}%",
                ["Status_DownloadFailed"] = "Download fehlgeschlagen: {0}",
                ["Status_Scanning"] = "Scanne...",
                ["Status_SetGameDir"] = "Bitte Spielverzeichnis festlegen",
                ["Status_GameStarted"] = "Spiel gestartet",
                ["Status_WebOpened"] = "Webseite geöffnet",
                ["Status_ModuleEnabled"] = "{0} aktiviert",
                ["Status_ModuleDisabled"] = "{0} deaktiviert",
                ["Status_CacheCleared"] = "{0} Cache-Dateien gelöscht ({1})",
                ["Status_RefreshFailed"] = "Aktualisierung fehlgeschlagen: {0}",
                ["Action_Download"] = "Herunterladen & Installieren",
                ["Action_Downloading"] = "Wird heruntergeladen...",
                ["Action_Paused"] = "Pausiert",
                ["Action_Resume"] = "Fortsetzen ({0})",
                ["Action_Cancel"] = "Abbrechen",
                ["Action_Enable"] = "Aktivieren",
                ["Action_Disable"] = "Deaktivieren",
                ["Action_Disabled"] = "Deaktiviert",
                ["Action_Update"] = "Aktualisieren auf {0}",
                ["Action_Latest"] = "Aktuell",
                ["Module_NoDescription"] = "Keine Beschreibung",
                ["Module_Dependencies"] = "Abhängigkeiten: {0}",
                ["Module_LocalVersion"] = "Lokal: {0}",
                ["Module_LatestVersionUnknown"] = "Neueste: wird geladen...",
                ["Module_LatestVersion"] = "Neueste: {0}",
                ["Module_VersionUnknown"] = "Unbekannt",
                ["Module_VersionTooltip"] = "Version unbekannt, klicken Sie auf Download zum Aktualisieren",
                ["Module_SizeMB"] = "{0:F1} MB",
                ["Msg_WelcomeTitle"] = "Willkommen",
                ["Msg_ErrorTitle"] = "Fehler",
                ["Msg_InfoTitle"] = "Information",
                ["Msg_ConfirmTitle"] = "Bestätigen",
                ["Msg_Welcome"] = "Willkommen beim Project Reforged Manager!\n\nBitte legen Sie zuerst das Spielverzeichnis fest.",
                ["Msg_SetGameDir"] = "Bitte Spielverzeichnis festlegen",
                ["Msg_GameNotFound"] = "WoW.exe nicht gefunden, bitte Verzeichnis prüfen",
                ["Msg_SelectWowExe"] = "Bitte wählen Sie WoW.exe",
                ["Msg_DataDirNotExist"] = "Spielverzeichnis existiert nicht, bitte korrekten Pfad angeben",
                ["Msg_NoTempFiles"] = "Keine temporären Dateien zum Bereinigen",
                ["Msg_ClearFailed"] = "Bereinigung fehlgeschlagen: {0}",
                ["Msg_LoadError"] = "Daten laden fehlgeschlagen: {0}",
                ["Msg_DownloadFailed"] = "Download fehlgeschlagen: {0}",
                ["Msg_FileNotFound"] = "Datei nicht gefunden: {0}",
                ["Msg_OperationFailed"] = "Vorgang fehlgeschlagen: {0}",
                ["Msg_LaunchFailed"] = "Starten fehlgeschlagen: {0}",
                ["Msg_OpenWebFailed"] = "Webseite öffnen fehlgeschlagen: {0}",
                ["Msg_ConfirmClearCache"] = "{0} unvollständige Downloads gefunden, bereinigen?\n\nGesamtgröße: {1:F1} MB",
                ["Msg_CacheCleared"] = "{0} Dateien bereinigt\nFreier Speicher: {1}",
                ["Msg_MissingDeps"] = "Fehlende Abhängigkeiten: {0}\n\nBitte zuerst herunterladen",
                ["Msg_CannotEnable"] = "Kann {0} nicht aktivieren\n\nFehlende Abhängigkeiten:\n{1}\n\nBitte zuerst herunterladen und installieren.",
                ["Msg_StopDownload"] = "Aktive Downloads vorhanden.\nDownload stoppen und beenden?",
                ["Msg_StopDownloadTitle"] = "Beenden bestätigen",
                
                // Systemabhängigkeiten
                ["Msg_MissingSystemDeps"] = "Fehlende Systemabhängigkeiten:\n{0}\n\nBitte starten Sie den offiziellen Launcher und installieren Sie VanillaHelpers und DXVK auf der Mods-Seite.",
                ["Msg_DisabledSystemDeps"] = "Die folgenden Systemabhängigkeiten sind deaktiviert:\n{0}\n\nBitte starten Sie den offiziellen Launcher und aktivieren Sie VanillaHelpers und DXVK auf der Mods-Seite.",
                ["Status_MissingSystemDeps"] = "Systemabhängigkeiten fehlen, bitte im offiziellen Launcher installieren",
                ["Status_DisabledSystemDeps"] = "Systemabhängigkeiten deaktiviert, bitte im offiziellen Launcher aktivieren",
                
                // Download Status
                ["Dl_DownloadingFiles"] = "Lade {0} Dateien ({1} aktiv, {2} pausiert), Fortschritt: {3:F0}%",
                ["Dl_SingleDownloading"] = "Lade: {0}, Fortschritt: {1:F0}%",
                ["Dl_SinglePaused"] = "Pausiert: {0}",

                // GPU messages
                ["Msg_AmdOnly"] = "AMD GPU detected. Use DXVK 2.6 or earlier. Before launch, switch to the system English input method and keep it active.",
                ["Msg_AmdNvidiaMixed"] = "Discrete NVIDIA and integrated AMD GPUs detected. Launch the game with the discrete NVIDIA GPU to avoid compatibility issues."
            };

            // 葡萄牙语 (pt)
            translations["pt"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "Gerenciador Project Reforged",
                ["Settings_Title"] = "Configurações",
                ["Settings_GamePath"] = "Selecionar WoW.exe",
                ["Settings_Browse"] = "Procurar...",
                ["Settings_InstallPath"] = "Caminho de instalação",
                ["Settings_Language"] = "Idioma",
                ["Settings_ClearCache"] = "Limpar cache",
                ["Settings_Author"] = "Autor: Cliencer",
                ["Settings_4GBPatch"] = "Patch 4GB",
                ["Section_Core"] = "Módulos principais",
                ["Section_Optional"] = "Melhorias opcionais",
                ["Section_Audio"] = "Melhorias de áudio",
                ["Section_Ultra"] = "Texturas Ultra HD",
                ["Status_Ready"] = "Pronto",
                ["Status_Loading"] = "Carregando {0} módulos...",
                ["Status_Loaded"] = "Carregados {0} módulos",
                ["Status_LoadedLocal"] = ", {0} patches locais encontrados",
                ["Status_LoadedUnknown"] = " ({0} arquivos desconhecidos)",
                ["Status_LoadFailed"] = "Falha ao carregar: {0}",
                ["Status_Downloading"] = "Baixando {0}... {1}%",
                ["Status_DownloadFailed"] = "Falha no download: {0}",
                ["Status_Scanning"] = "Verificando...",
                ["Status_SetGameDir"] = "Por favor configure o diretório do jogo",
                ["Status_GameStarted"] = "Jogo iniciado",
                ["Status_WebOpened"] = "Página web aberta",
                ["Status_ModuleEnabled"] = "{0} ativado",
                ["Status_ModuleDisabled"] = "{0} desativado",
                ["Status_CacheCleared"] = "Limpos {0} arquivos de cache ({1})",
                ["Status_RefreshFailed"] = "Falha ao atualizar: {0}",
                ["Action_Download"] = "Baixar e instalar",
                ["Action_Downloading"] = "Baixando...",
                ["Action_Paused"] = "Pausado",
                ["Action_Resume"] = "Continuar ({0})",
                ["Action_Cancel"] = "Cancelar",
                ["Action_Enable"] = "Ativar",
                ["Action_Disable"] = "Desativar",
                ["Action_Disabled"] = "Desativado",
                ["Action_Update"] = "Atualizar para {0}",
                ["Action_Latest"] = "Mais recente",
                ["Module_NoDescription"] = "Sem descrição",
                ["Module_Dependencies"] = "Dependências: {0}",
                ["Module_LocalVersion"] = "Local: {0}",
                ["Module_LatestVersionUnknown"] = "Mais recente: carregando...",
                ["Module_LatestVersion"] = "Mais recente: {0}",
                ["Module_VersionUnknown"] = "Desconhecida",
                ["Module_VersionTooltip"] = "Versão desconhecida, clique em baixar para atualizar",
                ["Module_SizeMB"] = "{0:F1} MB",
                ["Msg_WelcomeTitle"] = "Bem-vindo",
                ["Msg_ErrorTitle"] = "Erro",
                ["Msg_InfoTitle"] = "Informação",
                ["Msg_ConfirmTitle"] = "Confirmar",
                ["Msg_Welcome"] = "Bem-vindo ao Gerenciador Project Reforged!\n\nPor favor configure o diretório do jogo primeiro.",
                ["Msg_SetGameDir"] = "Por favor configure o diretório do jogo",
                ["Msg_GameNotFound"] = "WoW.exe não encontrado, verifique o diretório",
                ["Msg_SelectWowExe"] = "Selecione WoW.exe",
                ["Msg_DataDirNotExist"] = "Diretório não existe, configure o caminho correto",
                ["Msg_NoTempFiles"] = "Nenhum arquivo temporário para limpar",
                ["Msg_ClearFailed"] = "Falha ao limpar: {0}",
                ["Msg_LoadError"] = "Falha ao carregar dados: {0}",
                ["Msg_DownloadFailed"] = "Falha no download: {0}",
                ["Msg_FileNotFound"] = "Arquivo não encontrado: {0}",
                ["Msg_OperationFailed"] = "Operação falhou: {0}",
                ["Msg_LaunchFailed"] = "Falha ao iniciar: {0}",
                ["Msg_OpenWebFailed"] = "Falha ao abrir página web: {0}",
                ["Msg_ConfirmClearCache"] = "Encontrados {0} arquivos de download incompletos, limpar?\n\nTamanho total: {1:F1} MB",
                ["Msg_CacheCleared"] = "Limpos {0} arquivos\nEspaço liberado: {1}",
                ["Msg_MissingDeps"] = "Dependências ausentes: {0}\n\nBaixe-as primeiro",
                ["Msg_CannotEnable"] = "Não é possível ativar {0}\n\nDependências ausentes:\n{1}\n\nBaixe-as e instale primeiro.",
                ["Msg_StopDownload"] = "Há downloads ativos.\nParar downloads e sair?",
                ["Msg_StopDownloadTitle"] = "Confirmar Saída",
                
                // Dependências do Sistema
                ["Msg_MissingSystemDeps"] = "Arquivos de dependência do sistema ausentes:\n{0}\n\nInicie o launcher oficial e instale o VanillaHelpers e DXVK na página de Mods.",
                ["Msg_DisabledSystemDeps"] = "As seguintes dependências do sistema estão desabilitadas:\n{0}\n\nInicie o launcher oficial e habilite o VanillaHelpers e DXVK na página de Mods.",
                ["Status_MissingSystemDeps"] = "Dependências do sistema ausentes, instale-as no launcher oficial",
                ["Status_DisabledSystemDeps"] = "Dependências do sistema desabilitadas, habilite-as no launcher oficial",
                
                // Download Status
                ["Dl_DownloadingFiles"] = "Baixando {0} arquivos ({1} ativos, {2} pausados), progresso: {3:F0}%",
                ["Dl_SingleDownloading"] = "Baixando: {0}, progresso: {1:F0}%",
                ["Dl_SinglePaused"] = "Pausado: {0}",

                // GPU messages
                ["Msg_AmdOnly"] = "GPU AMD detectada. Use DXVK 2.6 ou anterior. Antes de iniciar, altere para o método de entrada em inglês do sistema e mantenha-o ativo.",
                ["Msg_AmdNvidiaMixed"] = "Detectadas GPUs NVIDIA (discreta) e AMD (integrada). Inicie o jogo com a GPU NVIDIA discreta para evitar problemas de compatibilidade."
            };

            // 俄语 (ru)
            translations["ru"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "Менеджер Project Reforged",
                ["Settings_Title"] = "Настройки",
                ["Settings_GamePath"] = "Выбрать WoW.exe",
                ["Settings_Browse"] = "Обзор...",
                ["Settings_InstallPath"] = "Путь установки",
                ["Settings_Language"] = "Язык",
                ["Settings_ClearCache"] = "Очистить кэш",
                ["Settings_Author"] = "Автор: Cliencer",
                ["Settings_4GBPatch"] = "Патч 4GB",
                ["Section_Core"] = "Основные модули",
                ["Section_Optional"] = "Дополнительные улучшения",
                ["Section_Audio"] = "Улучшения звука",
                ["Section_Ultra"] = "Ultra HD текстуры",
                ["Status_Ready"] = "Готово",
                ["Status_Loading"] = "Загрузка {0} модулей...",
                ["Status_Loaded"] = "Загружено {0} модулей",
                ["Status_LoadedLocal"] = "， найдено {0} локальных патчей",
                ["Status_LoadedUnknown"] = " (найдено {0} неизвестных файлов)",
                ["Status_LoadFailed"] = "Ошибка загрузки: {0}",
                ["Status_Downloading"] = "Загрузка {0}... {1}%",
                ["Status_DownloadFailed"] = "Ошибка загрузки: {0}",
                ["Status_Scanning"] = "Сканирование...",
                ["Status_SetGameDir"] = "Пожалуйста, укажите папку с игрой",
                ["Status_GameStarted"] = "Игра запущена",
                ["Status_WebOpened"] = "Веб-страница открыта",
                ["Status_ModuleEnabled"] = "{0} включен",
                ["Status_ModuleDisabled"] = "{0} отключен",
                ["Status_CacheCleared"] = "Очищено {0} файлов кэша ({1})",
                ["Status_RefreshFailed"] = "Ошибка обновления: {0}",
                ["Action_Download"] = "Скачать и установить",
                ["Action_Downloading"] = "Загрузка...",
                ["Action_Paused"] = "Приостановлено",
                ["Action_Resume"] = "Продолжить ({0})",
                ["Action_Cancel"] = "Отмена",
                ["Action_Enable"] = "Включить",
                ["Action_Disable"] = "Отключить",
                ["Action_Disabled"] = "Отключено",
                ["Action_Update"] = "Обновить до {0}",
                ["Action_Latest"] = "Последняя версия",
                ["Module_NoDescription"] = "Нет описания",
                ["Module_Dependencies"] = "Зависимости: {0}",
                ["Module_LocalVersion"] = "Локально: {0}",
                ["Module_LatestVersionUnknown"] = "Последняя: загрузка...",
                ["Module_LatestVersion"] = "Последняя: {0}",
                ["Module_VersionUnknown"] = "Неизвестно",
                ["Module_VersionTooltip"] = "Версия неизвестна, нажмите загрузить для обновления",
                ["Module_SizeMB"] = "{0:F1} МБ",
                ["Msg_WelcomeTitle"] = "Добро пожаловать",
                ["Msg_ErrorTitle"] = "Ошибка",
                ["Msg_InfoTitle"] = "Информация",
                ["Msg_ConfirmTitle"] = "Подтвердить",
                ["Msg_Welcome"] = "Добро пожаловать в Менеджер Project Reforged!\n\nПожалуйста, сначала укажите папку с игрой.",
                ["Msg_SetGameDir"] = "Пожалуйста, укажите папку с игрой",
                ["Msg_GameNotFound"] = "WoW.exe не найден, проверьте папку с игрой",
                ["Msg_SelectWowExe"] = "Выберите WoW.exe",
                ["Msg_DataDirNotExist"] = "Папка с игрой не существует, укажите правильный путь",
                ["Msg_NoTempFiles"] = "Нет временных файлов для очистки",
                ["Msg_ClearFailed"] = "Ошибка очистки: {0}",
                ["Msg_LoadError"] = "Ошибка загрузки данных: {0}",
                ["Msg_DownloadFailed"] = "Ошибка загрузки: {0}",
                ["Msg_FileNotFound"] = "Файл не найден: {0}",
                ["Msg_OperationFailed"] = "Операция не удалась: {0}",
                ["Msg_LaunchFailed"] = "Ошибка запуска: {0}",
                ["Msg_OpenWebFailed"] = "Ошибка открытия веб-страницы: {0}",
                ["Msg_ConfirmClearCache"] = "Найдено {0} незавершенных загрузок, очистить?\n\nОбщий размер: {1:F1} МБ",
                ["Msg_CacheCleared"] = "Очищено {0} файлов\nОсвобождено: {1}",
                ["Msg_MissingDeps"] = "Отсутствуют зависимости: {0}\n\nСначала скачайте их",
                ["Msg_CannotEnable"] = "Невозможно включить {0}\n\nОтсутствуют зависимости:\n{1}\n\nСначала скачайте и установите их.",
                ["Msg_StopDownload"] = "Есть активные загрузки.\nОстановить загрузки и выйти?",
                ["Msg_StopDownloadTitle"] = "Подтвердить выход",

                // GPU messages
                ["Msg_AmdOnly"] = "Обнаружена видеокарта AMD. Используйте DXVK 2.6 или ниже. Перед запуском переключитесь на системную английскую раскладку и держите её активной.",
                ["Msg_AmdNvidiaMixed"] = "Обнаружены дискретная NVIDIA и интегрированная AMD. Запускайте игру на дискретной NVIDIA, чтобы избежать проблем совместимости."
            };

            return translations;
        }

        /// <summary>
        /// 获取默认语言（根据系统语言，无法确认则默认英语）
        /// </summary>
        private static CultureInfo GetDefaultCulture()
        {
            var systemCulture = CultureInfo.CurrentUICulture;
            // 检查是否为中文（包括简体、繁体等）
            if (systemCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return new CultureInfo("zh-CN");
            }
            // 其他情况默认英语
            return new CultureInfo("en");
        }

        /// <summary>
        /// 获取支持的语言代码列表
        /// </summary>
        public static string[] SupportedLanguages => new[] { "zh-CN", "en", "es", "de", "pt", "ru" };

        /// <summary>
        /// 获取语言显示名称
        /// </summary>
        public static string GetLanguageDisplayName(string cultureName)
        {
            return cultureName?.ToLowerInvariant() switch
            {
                "zh-cn" => "中文",
                "en" => "English",
                "es" => "Español",
                "de" => "Deutsch",
                "pt" => "Português",
                "ru" => "Русский",
                _ => cultureName
            };
        }

        /// <summary>
        /// 检查是否支持该语言
        /// </summary>
        public static bool IsSupportedLanguage(string cultureName)
        {
            if (string.IsNullOrEmpty(cultureName))
                return false;
            return SupportedLanguages.Contains(cultureName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取默认语言代码（根据系统语言，无法确认则默认英语）
        /// </summary>
        public static string GetDefaultLanguage()
        {
            var systemCulture = CultureInfo.CurrentUICulture;
            // 检查是否为中文（包括简体、繁体等）
            if (systemCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-CN";
            }
            // 其他情况默认英语
            return "en";
        }

        /// <summary>
        /// 当前文化
        /// </summary>
        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            private set
            {
                _currentCulture = value;
                Thread.CurrentThread.CurrentCulture = value;
                Thread.CurrentThread.CurrentUICulture = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
            }
        }

        /// <summary>
        /// 当前语言代码
        /// </summary>
        public string CurrentLanguage => _currentCulture?.Name ?? "en";

        /// <summary>
        /// 获取本地化字符串
        /// </summary>
        public string this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                    return key;

                var langKey = CurrentLanguage;
                
                // 尝试当前语言
                if (_translations.TryGetValue(langKey, out var currentLang) && 
                    currentLang.TryGetValue(key, out var value))
                {
                    return value;
                }

                // 回退到英语
                if (langKey != "en" && _translations.TryGetValue("en", out var enLang) && 
                    enLang.TryGetValue(key, out var enValue))
                {
                    return enValue;
                }

                // 最后回退到中文（默认资源）
                if (_translations.TryGetValue("zh-CN", out var zhLang) && 
                    zhLang.TryGetValue(key, out var zhValue))
                {
                    return zhValue;
                }

                // 如果都找不到，返回键名本身
                return key;
            }
        }

        /// <summary>
        /// 获取格式化后的本地化字符串
        /// </summary>
        public string GetString(string key, params object[] args)
        {
            var format = this[key];
            return args.Length > 0 ? string.Format(format, args) : format;
        }

        /// <summary>
        /// 切换语言
        /// </summary>
        public void ChangeLanguage(string cultureName)
        {
            if (string.IsNullOrEmpty(cultureName))
                return;

            // 验证是否支持该语言，不支持则使用默认语言
            if (!IsSupportedLanguage(cultureName))
            {
                cultureName = GetDefaultLanguage();
            }

            try
            {
                var newCulture = new CultureInfo(cultureName);
                CurrentCulture = newCulture;
            }
            catch (CultureNotFoundException)
            {
                // 如果指定的文化不存在，回退到英语
                CurrentCulture = new CultureInfo("en");
            }
        }
    }

    /// <summary>
    /// 本地化转换器 (用于 XAML 绑定)
    /// </summary>
    public class LocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string key)
            {
                return LocalizationService.Instance[key];
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 本地化标记扩展 (用于 XAML)
    /// </summary>
    public class LocExtension : Binding
    {
        public LocExtension(string name) : base($"[{name}]")
        {
            Mode = BindingMode.OneWay;
            Source = LocalizationService.Instance;
        }
    }
}
