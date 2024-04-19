using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PluginInterface;

namespace PluginLibrary//20231225
{
    //1. 定义插件接口: 首先，定义一个插件接口。这个接口将是所有插件必须实现的合同
    //public interface IPlugin
    //{
    //    string Name { get; }
    //    void Execute();
    //}

    //2. 创建插件宿主模块:
    public class PluginHost 
    {
        private List<IAlgorithmPlugin> plugins = new List<IAlgorithmPlugin>();
        
        //修改 PluginHost 类以提供一个方法来获取所有可用插件的信息。
        public IEnumerable<IAlgorithmPlugin> AvailablePlugins => plugins;//扩展 PluginHost 以获取插件信息
        


        public void LoadPlugins(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*.dll"))
            {
                string fullPath = Path.GetFullPath(file);
                var assembly = Assembly.LoadFile(fullPath);
                // 使用反射创建 MyClass 的实例
                //Type[] myType = assembly.GetTypes();
                //object myObject = Activator.CreateInstance(myType[0]);

                foreach (var atype in assembly.GetTypes())
                {
                    try 
                    {
                        if (typeof(IAlgorithmPlugin).IsAssignableFrom(atype)&&
                            !atype.IsInterface // 确保不是接口
                            && !atype.IsAbstract) // 确保不是抽象类
                        {
                            //IPlugin plugin = Activator.CreateInstance(type) as IPlugin;
                            /*IPlugin*/
                            object plugin = Activator.CreateInstance(atype) ;
                            MessageBox.Show($"Type: {plugin.GetType().FullName}, Assembly: {plugin.GetType().Assembly.FullName}");
                            plugins.Add((IAlgorithmPlugin)plugin);

                        }
                    }
                    catch (Exception e) 
                    {
                    }
 
                }
            }
        }

        public void ExecutePlugins()
        {
            foreach (var plugin in plugins)
            {
                plugin.Execute();
            }
        }
    }


}
