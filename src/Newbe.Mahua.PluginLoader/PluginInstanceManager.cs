﻿using Newbe.Mahua.Commands;
using Newbe.Mahua.Domains;
using Newbe.Mahua.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Newbe.Mahua
{
    /// <summary>
    /// 插件实例管理器
    /// </summary>
    public static class PluginInstanceManager
    {
        private const string AssetDirName = "YUELUO";
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(PluginInstanceManager));

        public static IPluginLoader GetInstance(PluginFileInfo pluginFileInfo)
        {
            try
            {
                EnsureAppDomainInitialized(pluginFileInfo);
                return Instances[pluginFileInfo.Name].PluginLoader;
            }
            catch (Exception e)
            {
                Logger.ErrorException(e.Message, e);
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            return null;
        }

        private static IDictionary<string, PluginInstance> Instances { get; } =
            new Dictionary<string, PluginInstance>();

        private static void EnsureAppDomainInitialized(PluginFileInfo pluginFileInfo)
        {
            var pluginInfoName = pluginFileInfo.Name;
            if (Instances.ContainsKey(pluginInfoName))
            {
                return;
            }

            Logger.Info($"当前机器人平台为：{MahuaGlobal.CurrentPlatform:G}");
            Logger.Info("开始加载插件");
            Logger.Debug(pluginFileInfo.ToString());
            Logger.Debug($"当前插件名称为{pluginInfoName}");
            Logger.Debug($"开始复制插件Asset文件 : {pluginInfoName}");
            var pluginAssetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetDirName, pluginInfoName);
            var pluginRuntimeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginInfoName);
            if (Directory.Exists(pluginRuntimeDir))
            {
                Directory.Delete(pluginRuntimeDir, true);
            }

            Directory.CreateDirectory(pluginRuntimeDir);
            foreach (var fileFullname in Directory.GetFiles(pluginAssetDir))
            {
                var filename = Path.GetFileName(fileFullname);
                File.Copy(fileFullname, Path.Combine(pluginRuntimeDir, filename));
            }
            Logger.Debug($"复制Asset文件完毕 : {pluginInfoName}");
            var domainLoader = new DomainLoader(
                pluginInfoName,
                pluginFileInfo.PluginEntyPointDirectory,
                pluginFileInfo.PluginEntryPointConfigFullFilename,
                true);
            Logger.Debug($"创建AppDomain进行加载插件:{pluginInfoName}");
            domainLoader.Load();
            Logger.Debug("开始创建透明代理");
            var loader = domainLoader.Create<IPluginLoader>(typeof(CrossAppDomainPluginLoader).FullName);
            Logger.Debug(
                $"透明代理创建完毕，类型为{loader.GetType().FullName}，将开始调用{nameof(CrossAppDomainPluginLoader.LoadPlugin)}方法");
            if (!loader.LoadPlugin(pluginFileInfo.PluginEntryPointDllFullFilename))
            {
                throw new PluginLoadException(pluginInfoName, loader.Message);
            }

            var pluginInstance = new PluginInstance
            {
                DomainLoader = domainLoader,
                PluginLoader = loader,
                PluginFileInfo = pluginFileInfo,
            };
            var watcher = new FileSystemWatcher
            {
                Path = Path.Combine(pluginAssetDir),
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "hash.txt"
            };
            watcher.Changed += Watcher_Changed;
            watcher.EnableRaisingEvents = true;
            pluginInstance.FileSystemWatcher = watcher;
            Instances.Add(pluginInfoName, pluginInstance);
        }

        private static void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            var pluginName = Path.GetFileName(Path.GetDirectoryName(e.FullPath));
            Debug.Assert(pluginName != null, nameof(pluginName) + " != null");

            Task.Run(() =>
           {
               if (!Instances.ContainsKey(pluginName))
               {
                   return;
               }
               var pluginInstance = Instances[pluginName];
               lock (pluginInstance.PluginFileInfo)
               {
                   // 已经更新后Domain发生变化，因此无需再次更新
                   if (Instances[pluginName].DomainLoader != pluginInstance.DomainLoader)
                   {
                       return;
                   }

                   Logger.Info($"{pluginName} 发现 hash.txt 文件变更，启动热更新。");
                   try
                   {
                       Logger.Info($"{pluginName} 热更新启动...");
                       var re = pluginInstance.PluginLoader
                           .SendCommand<PluginHotUpgradingCommand, PluginHotUpgradingCommandResult>(
                               new PluginHotUpgradingCommand());
                       if (re.Canceled)
                       {
                           Logger.Info($"{pluginName} 热更新被取消，原因：{re.Reason}");
                           return;
                       }

                       DisposeAppDomain(pluginName);
                       GetInstance(pluginInstance.PluginFileInfo).SendCommand(new PluginHotUpgradedCommand());
                       Logger.Info($"{pluginName} 热更新完毕.");
                   }
                   catch (Exception exception)
                   {
                       Logger.ErrorException($"{pluginName} 热更新失败！", exception);
                       throw;
                   }
               }
           });
        }

        internal static void DisposeAppDomain(string pluginName)
        {
            if (Instances.ContainsKey(pluginName))
            {
                var pluginInstance = Instances[pluginName];
                pluginInstance.DomainLoader.Dispose();
                pluginInstance.DomainLoader = null;
                pluginInstance.PluginLoader = null;
                pluginInstance.FileSystemWatcher.Dispose();
                Instances.Remove(pluginName);
            }
        }

        private class PluginInstance
        {
            public PluginFileInfo PluginFileInfo { get; set; }
            public DomainLoader DomainLoader { get; set; }
            public IPluginLoader PluginLoader { get; set; }
            public FileSystemWatcher FileSystemWatcher { get; set; }
        }
    }
}
