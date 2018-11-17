using System;
using System.IO;
using System.Reflection;

namespace Bot {
    public class Logger {
        private static string logFile = null;        

        private static void Initialize() {
            logFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Logs", DateTime.UtcNow.ToString("yyyy-MM-dd HH.mm.ss.ffff") + ".log");
            Directory.CreateDirectory(Path.GetDirectoryName(logFile));            
        }
        
        public static void Info(string line, params object[] parameters) {
            if (logFile == null) Initialize();
                    
            string msg = "[" + DateTime.UtcNow.ToString("HH:mm:ss") + " INFO] " + String.Format(line, parameters); 
            System.IO.StreamWriter file = new System.IO.StreamWriter(logFile, true);
            file.WriteLine(msg);
            file.Close();
            Console.WriteLine(msg, parameters);
        }
    }
}