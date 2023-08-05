using System.Diagnostics;
using System.Resources;
using System.Runtime.InteropServices;
namespace HotKey
{

    partial class Program
    {
        [DllImport ( "user32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
        private static extern IntPtr SetWindowsHookEx ( int idHook, 
                                                        LowLevelKeyboardProc lpfn, 
                                                        IntPtr hMod, 
                                                        uint dwThreadId ) ;

        [DllImport ( "user32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
        [return: MarshalAs ( UnmanagedType.Bool ) ]
        private static extern bool UnhookWindowsHookEx ( IntPtr hhk ) ;

        [DllImport ( "user32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
        private static extern IntPtr CallNextHookEx (   IntPtr hhk, 
                                                        int nCode, 
                                                        IntPtr wParam, 
                                                        IntPtr lParam ) ;

        [DllImport ( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
        private static extern IntPtr GetModuleHandle ( string lpModuleName ) ;

        private const int WH_KEYBOARD_LL = 13;                    //Type of Hook - Low Level Keyboard
        private const int WM_KEYDOWN = 0x0100;                    //Value passed on KeyDown
        private const int WM_KEYUP = 0x0101;                      //Value passed on KeyUp
        private static LowLevelKeyboardProc _proc = HookCallback; //The function called when a key is pressed
        private static IntPtr hookID = IntPtr.Zero;

        private static NotifyIcon TrayIcon = new NotifyIcon
                                                            {
                                                                Text    = "Hotkeys service",
                                                                Icon    = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                                                                Visible = true
                                                            };



        public static void Main (  ) 
        {
            Configuration.LoadCFG (  ) ;
            hookID = SetHook ( _proc ) ;                               //Set our hook
            

            
            Application.Run (  ) ;                                      //Start a standard application method loop
        }

        private static IntPtr SetHook ( LowLevelKeyboardProc proc ) 
        {
            using  ( Process curProcess = Process.GetCurrentProcess (  )  ) 
            using  ( ProcessModule curModule = curProcess.MainModule ) 
            {
                return SetWindowsHookEx ( WH_KEYBOARD_LL, proc, GetModuleHandle ( curModule.ModuleName ) , 0 ) ;
            }
        }

        private static List<string> KeysDown = new List<string> (  ) ;
        static Nullable<HotKey> exec;

        private delegate IntPtr LowLevelKeyboardProc ( int nCode, IntPtr wParam, IntPtr lParam ) ;

        private static IntPtr HookCallback ( int nCode, IntPtr wParam, IntPtr lParam ) 
        {
            if  ( nCode >= 0 && wParam ==  ( IntPtr )  WM_KEYDOWN )                 //Pressed
            {
                int vkCode      = Marshal.ReadInt32 ( lParam ) ;                    //Get the keycode
                string theKey   =  (  ( Keys ) vkCode ) .ToString (  ) ;            //Name of the key

                if (!KeysDown.Contains(theKey.ToUpper())) KeysDown.Add(theKey.ToUpper());

                exec = Configuration.Check ( KeysDown ) ;


                if (exec is not null)
                {

                    //Debug.WriteLine(exec.Value.ExecPath);
                    //Debug.WriteLine(exec.Value.Arguments);
                    Process process = new Process();
                    process.StartInfo.UseShellExecute       = exec.Value.UseShellExecute;
                    process.StartInfo.WindowStyle           = exec.Value.WindowStyle;
                    process.StartInfo.RedirectStandardError = false;
                    process.StartInfo.RedirectStandardOutput= false;
                    process.StartInfo.CreateNoWindow        = exec.Value.CreateNoWindow;
                    process.StartInfo.RedirectStandardInput = false;
                    process.StartInfo.FileName              = exec.Value.ExecPath;
                    process.StartInfo.Arguments             = exec.Value.Arguments;
                    //process.StartInfo.UserName = "Администратор";
                    //process.StartInfo.Password = testString;
                    process.StartInfo.Domain = Environment.MachineName;

                    process.Start();
                }
                if  ( theKey == "Escape" )                                          //If press escape
                {
                    UnhookWindowsHookEx ( hookID ) ;                                //Release our hook
                    Environment.Exit ( 0 ) ;                                        //Correct exit
                }
            }
            else if  ( nCode >= 0 && wParam ==  ( IntPtr )  WM_KEYUP )              //Released
            {
                int vkCode      = Marshal.ReadInt32 ( lParam ) ;                    //Get Keycode
                string theKey   =  (  ( Keys ) vkCode ) .ToString (  ) ;            //Get Key name

               // KeysDown.Remove ( theKey.ToUpper() ) ;
                KeysDown.Clear();
            }
            return CallNextHookEx ( hookID, nCode, wParam, lParam ) ;               //Call the next hook
        }
    }

    public static class Extensions                                                  //For comfort only
    {
        /// <summary>
        /// Delete character
        /// </summary>
        /// <param name="str"></param>
        /// <param name="c"></param>
        /// <returns>New string without deleted characters</returns>
        public static string Delete(this string str, char c)
        {
            string newstring = string.Empty;
            foreach (char ch in str)
                if (ch != c)
                    newstring += ch;
            return newstring;
        }

        /// <summary>
        /// short name for search in S; {"LWIN" "LSHIFT"} in list and {"WIN" "SHIFT"} in array S
        /// </summary>
        /// <returns>true if all strings from this List contains all strings from S </returns>
        public static bool IsListContainced<T> (this IList<T> F, string[] S)
        {
            foreach (string f in F as List<string>)
            {
                bool notFound = true;
                foreach (string s in S)
                    if (f == s)
                    {
                        notFound = false;
                        break;
                    }
                if (notFound)
                    return false;
            }
            return true;
        }
    }


    struct HotKey
    {
        public readonly string[]    KeyNames;
        public readonly string      ExecPath;
        public readonly string      Arguments;
        public readonly bool        UseShellExecute;
        public readonly bool        CreateNoWindow;
        public readonly ProcessWindowStyle WindowStyle;
        public HotKey(ref string[] KeyNames, 
                      ref string ExecPath, 
                      ref string Arguments, 
                      ref bool UseShellExecute, 
                      ref bool CreateNoWindow,
                      ref ProcessWindowStyle Style)
        {
            this.KeyNames       = KeyNames;
            this.ExecPath       = ExecPath;
            this.Arguments      = Arguments;
            this.UseShellExecute= UseShellExecute;
            this.CreateNoWindow = CreateNoWindow;
            this.WindowStyle    = Style;
        }
    }

    static class Configuration
    {
        static List<HotKey> HotKeys = new List<HotKey>();

        public static void LoadCFG(string path = ".\\Config.CFG")
        {
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    String line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        Debug.WriteLine(line);
                        if (line.Count() > 0 &&             //
                            line[0] != '#' &&               //
                            line.Split('\t').Count() >= 2)   //
                        {
                            String[] parts = line.Split('\t', 6);
                            string[] keynames = parts[0].ToUpper().Delete(' ').Split("+");
                            string filepath = parts[1];
                            string arguments;
                            bool useShellExecute = true, createNoWindow = true;
                            ProcessWindowStyle style = ProcessWindowStyle.Normal;
                            if (parts.Count() >= 3) 
                                arguments = parts[2];
                            else
                                arguments = string.Empty;

                            if (parts.Count() >= 4)
                                useShellExecute = bool.Parse(parts[3]);
                            if (parts.Count() >= 5)
                                createNoWindow = bool.Parse(parts[4]);
                            if (parts.Count() >= 6)
                                switch (int.Parse(parts[5]))
                                {
                                    case 0: style = ProcessWindowStyle.Normal;      break;
                                    case 1: style = ProcessWindowStyle.Hidden;      break;
                                    case 2: style = ProcessWindowStyle.Minimized;   break;
                                    case 3: style = ProcessWindowStyle.Maximized;   break;
                                }
                                
                            HotKey newHotKey = new HotKey(  ref keynames, 
                                                            ref filepath, 
                                                            ref arguments, 
                                                            ref useShellExecute, 
                                                            ref createNoWindow,
                                                            ref style);
                            HotKeys.Add(newHotKey);

                        }
                    }
                }
                Debug.WriteLine (" Загружено " + HotKeys.Count.ToString() + " ХОТКЕЕВ");
            }
            catch
            {
                Environment.Exit(1);
            }
        }

        public static Nullable<HotKey> Check(List<string> keysdown)
        {
            
            foreach (string key in keysdown) { Debug.Write(key); }
            Debug.WriteLine ( " Нажата \n");
            Debug.WriteLine ( " Всего " + HotKeys.Count.ToString() + " ХОТКЕЕВ" );
            foreach (var hotkey in HotKeys)
            {
                if (keysdown.Count() != hotkey.KeyNames.Count() ) 
                    continue;
           else if (keysdown.IsListContainced ( hotkey.KeyNames ) )
                    return hotkey;
            }
            return null;
        }

    }

}