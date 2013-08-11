using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AdvancedHookManaged;
using GTA;
using GTA.Native;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;


namespace IVVehicleSaver
{
    public class mainScript : Script
    {
        [Serializable]
        public struct IVVehicle
        {
            public float Fuel;
            public float x, y, z, h;
            public float Dirt;
            public int Color, Color1, Color2;
            public int ModelHash;
            public Guid ID;
        }

        #region Properties

        List<Guid> updateFuelIDS = new List<Guid>();
        /// <summary>
        /// Holds the latest request fuel value.
        /// </summary>
        int latestRequestedFuel = -1;
        /// <summary>
        /// Sets if is using Realistic Fuel Mod
        /// </summary>
        bool usingFuelScript = false;
        /// <summary>
        /// Realistic Fuel Script GUID
        /// </summary>
        Guid FuelScriptGUID = new Guid("3583e09d-6c44-4820-85e9-93926307d4f8");
        /// <summary>
        /// List of saved vehicles
        /// </summary>
        List<IVVehicle> mainList = new List<IVVehicle>();
        /// <summary>
        /// Blip list
        /// </summary>
        Dictionary<Guid, Blip> blipList = new Dictionary<Guid, Blip>();
        /// <summary>
        /// To manage message display
        /// </summary>
        bool hasSeenHelpMessageOnThisVehicle = false;
        /// <summary>
        /// To manage vehicle session saving
        /// </summary>
        int SAVES_MADE;
        /// <summary>
        /// Used for logs
        /// </summary>
        internal static readonly string scriptName = "IVVehicleSaver";
        #endregion Properties

        /// <summary>
        /// The script entry point
        /// </summary>
        public mainScript()
        {
            BindKey(Keys.Q, () =>
                {
                    if (Player.Character.isInVehicle())
                        AddOrRemoveVehicle(Player.Character.CurrentVehicle);
                }
            );

            GUID = new Guid("392f87f6-5267-430e-b8ce-e4ede894b02e");
            // check if using Realistic Fuel Script
            if (File.Exists(Game.InstallFolder + "\\scripts\\FuelScript.net.dll"))
            {
                Log("Realistic Fuel Mod", "FuelScript.net.dll present in scripts folder");
                usingFuelScript = true;
                BindScriptCommand("CurrentFuel", (s, p) =>
                    {
                        if (latestRequestedFuel != -1)
                        {
                            IVVehicle temp = mainList[latestRequestedFuel];
                            temp.Fuel = p.Convert<float>(0);
                            mainList[latestRequestedFuel] = temp;
                            latestRequestedFuel = -1;
                        }
                    }
                );
            }
            SAVES_MADE = Game.GetIntegerStatistic(IntegerStatistic.SAVES_MADE);
            BindConsoleCommand("ivvehiclesaver-load", (p) => { LoadFile(out mainList); });
            BindConsoleCommand("ivvehiclesaver-save", (p) => { SaveFile(); });

            Wait(5000);
            // Set timer interval time
            this.Interval = 1000;
            // Assign timer tick event
            this.Tick += new EventHandler(mainScript_Tick_Load);
            #region Log script start
            mainScript.Log(" - - - - - - - - - - - - - - - STARTUP - - - - - - - - - - - - - - - ", String.Format("GTA IV {0} under {1}", Game.Version.ToString(), mainScript.getOSInfo()));
            mainScript.Log("Started", String.Format("{0} v{1}", mainScript.scriptName, FileVersionInfo.GetVersionInfo(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".net.dll").ProductVersion, true));
            #endregion Log script start
        }

        /// <summary>
        /// First tick event takes care, of loading vehicles, and passes event handling to mainScript_Tick.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void mainScript_Tick_Load(object sender, EventArgs e)
        {
            try
            {
                LoadFile(out mainList);
                this.Tick -= new EventHandler(mainScript_Tick_Load);
                Wait(2000);
                this.Tick += new EventHandler(mainScript_Tick);
            }
            catch (Exception crap) { Log("mainScript_Tick_Load", crap.Message); }

        }
        /// <summary>
        /// Main tick event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void mainScript_Tick(object sender, EventArgs e)
        {
            #region auto-saving
            try
            {
                if (SAVES_MADE < Game.GetIntegerStatistic(IntegerStatistic.SAVES_MADE))
                {
                    SAVES_MADE = Game.GetIntegerStatistic(IntegerStatistic.SAVES_MADE);
                    SaveFile();
                }
            }
            catch (Exception crap) { Log("mainScript_Tick-AutoSaveHandler", crap.Message); }
            #endregion auto-saving

            #region button-handler
            try
            {
                if (Player.Character.isInVehicle() && Player.Character.CurrentVehicle.GetPedOnSeat(VehicleSeat.Driver) == Player.Character && Player.Character.CurrentVehicle.Speed < .8f)
                    // keyboard SPACE(57) + ARROW_DOWN(108)
                    // Gamepad A(16) + PAD_DOWN(9)
                    if (Function.Call<bool>("IS_BUTTON_PRESSED", 0, 14) && Function.Call<bool>("IS_BUTTON_PRESSED", 0, 9))
                        AddOrRemoveVehicle(Player.Character.CurrentVehicle);
                    else if (Game.isKeyPressed(Keys.Space) && Game.isKeyPressed(Keys.Down))
                        AddOrRemoveVehicle(Player.Character.CurrentVehicle);
            }
            catch (Exception crap) { Log("mainScript_Tick-KeyHandler", crap.Message); }
            #endregion button-handler

            #region message-displaying
            if (Settings.GetValueBool("DISPLAYHELP", "MISC", false))
                try
                {
                    if (Player.Character.isInVehicle() && Player.Character.CurrentVehicle.GetPedOnSeat(VehicleSeat.Driver) == Player.Character && Player.Character.CurrentVehicle.Speed < .8f)
                    {
                        if (!hasSeenHelpMessageOnThisVehicle)
                        {
                            if (GetGuid(Player.Character.CurrentVehicle) == Guid.Empty)
                                DisplayHelp("Hold ~INPUT_FRONTEND_X~ and ~INPUT_FRONTEND_DOWN~ to ~g~add~w~ the current vehicle to the ~y~saved vehicle list~w~.");
                            else
                                DisplayHelp("Hold ~INPUT_FRONTEND_X~ and ~INPUT_FRONTEND_DOWN~ to ~r~delete~w~ the current vehicle from the ~y~saved vehicle list~w~.");
                            hasSeenHelpMessageOnThisVehicle = true;
                        }
                    }
                    else
                    {
                        ClearHelp();
                        hasSeenHelpMessageOnThisVehicle = false;
                    }
                }
                catch (Exception crap) { Log("mainScript_Tick-MessageHandler", crap.Message); }
            #endregion message-displaying

            Guid pGUID = Guid.Empty;
            try
            {
                if (Player.Character.isInVehicle())
                    pGUID = GetGuid(Player.Character.CurrentVehicle);
                else
                    pGUID = GetGuid(Player.LastVehicle);

                int index = 0, temp_index = -1;
                IVVehicle temp = new IVVehicle();
                foreach (IVVehicle v in mainList)
                {
                    if (v.ID == pGUID)
                    {
                        Vehicle cVeh;

                        cVeh = Player.Character.CurrentVehicle;
                        if (Player.Character.isInVehicle())
                        {
                            if (usingFuelScript && !updateFuelIDS.Contains(v.ID))
                            {
                                SendScriptCommand(FuelScriptGUID, "SetCurrentFuel", new ParameterCollection(new string[] { v.Fuel.ToString() }));
                                updateFuelIDS.Add(v.ID);
                            }

                            if (usingFuelScript)
                            {
                                // Request fuel data
                                latestRequestedFuel = index;
                                SendScriptCommand(FuelScriptGUID, "GetCurrentFuel");
                            }
                        }
                        else
                            cVeh = Player.LastVehicle;
                        temp_index = index;
                        temp = v;
                        temp.x = cVeh.Position.X;
                        temp.y = cVeh.Position.Y;
                        temp.z = cVeh.Position.Z;
                        temp.h = cVeh.Heading;
                        break;
                    }
                    else
                        ForceVehicle(v);
                    index++;
                }
                if (temp_index != -1)
                    mainList[temp_index] = temp;
            }
            catch (Exception crap) { Log("mainScript_Tick-VehicleHandler", crap.Message + pGUID.ToString()); }
        }

        

        #region Methods
        /// <summary>
        /// Dump info about a vehicle in the log file
        /// </summary>
        /// <param name="veh"></param>
        void DumpVehInfo(IVVehicle veh, string message)
        {
            Log(message, String.Format("x:{0} y:{1} z:{2} h:{3} c:{4} c1:{5} c2:{6} f:{7} hash:{8} guid:{9} fuel:{10}", veh.x, veh.y, veh.z, veh.h, veh.Color, veh.Color1, veh.Color2, veh.Fuel, veh.ModelHash, veh.ID));
        }

        /// <summary>
        /// Populates a given list with the contents from IVVehicleSaver.xml
        /// </summary>
        /// <param name="res"></param>
        void LoadFile(out List<IVVehicle> res)
        {
            try
            {
                XmlSerializer xml = new XmlSerializer(typeof(List<IVVehicle>));
                if (File.Exists(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".xml"))
                {
                    using (FileStream fs = new FileStream(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".xml", FileMode.Open))
                    {
                        try
                        {
                            res = (List<IVVehicle>)xml.Deserialize(fs);
                            DisplayHelp("~y~IV Vehicle Saver~w~ loaded ~g~" + mainList.Count + "~w~ vehicles.");
                            Log("LoadFile", "Loaded " + res.Count + " vehicles.");
                        }
                        catch (Exception crap)
                        {
                            Log("LoadFile-SerializationError", crap.Message);
                            res = new List<IVVehicle>();
                        }
                        finally
                        {
                            fs.Close();
                        }
                    }
                }
                else
                    res = new List<IVVehicle>();
                
            }
            catch (Exception crap)
            {
                Log("LoadFile", crap.Message);
                res = new List<IVVehicle>();
            }
        }
        /// <summary>
        /// Saves mainList to IVVehicleSaver.xml, and backups existent file to IVVehicleSaver.backup.xml
        /// </summary>
        void SaveFile()
        {
            try
            {
                XmlSerializer xml = new XmlSerializer(typeof(List<IVVehicle>));
                if (File.Exists(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".xml"))
                {
                    if (File.Exists(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".backup.xml"))
                        File.Delete(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".backup.xml");
                    File.Copy(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".xml", Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".backup.xml");
                }
                using (FileStream fs = new FileStream(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".xml", FileMode.Create))
                {
                    try
                    {
                        xml.Serialize(fs, mainList);
                        DisplayHelp("~y~IV Vehicle Saver~w~ saved ~g~" + mainList.Count + "~w~ vehicles.");
                        Log("SaveFile", "Saved " + mainList.Count + " vehicles.");
                    }
                    catch (Exception crap) { Log("SaveFile-SerializationError", crap.Message); }
                    finally
                    {
                        fs.Close();
                    }
                }
            }
            catch (Exception crap) { Log("SaveFile", crap.Message); }
        }
        /// <summary>
        /// Returns the vehicle GUID if available, returns Guid.Empty if not available.
        /// </summary>
        /// <param name="veh"></param>
        /// <returns></returns>
        Guid GetGuid(Vehicle veh)
        {
            try
            {
                return veh.Metadata.GUID;
            }
            catch (Exception)
            {
                return Guid.Empty;
            }
        }
        /// <summary>
        /// Adds or removes a vehicle from the vehList.
        /// Returns:
        /// true - vehicle added
        /// false - vehicle removed
        /// </summary>
        /// <param name="veh"></param>
        /// <returns></returns>
        bool AddOrRemoveVehicle(Vehicle veh)
        {
            if (GetGuid(veh) == Guid.Empty)
            {
                AddVehicle(veh);
                return true;
            }
            else
            {
                RemoveVehicle(veh);
                return false;
            }
        }
        /// <summary>
        /// Removes a vehicle from the mainList and blipList
        /// </summary>
        /// <param name="veh"></param>
        void RemoveVehicle(Vehicle veh)
        {
            // search for the object reference
            int index = 0;
            foreach (IVVehicle v in mainList)
            {
                if (v.ID == veh.Metadata.GUID)
                {
                    // delete the blip
                    blipList[v.ID].Delete();
                    // remove blip from blipList
                    blipList.Remove(v.ID);
                    // Remove object from mainList
                    mainList.RemoveAt(index);
                    // Reset the Guid, so vehicle can be re-added.
                    veh.Metadata.GUID = Guid.Empty;
                    // Log the action
                    DumpVehInfo(v, "Removed Vehicle");
                    break;
                }
                index++;
            }

            DisplayHelp("Vehicle ~r~removed~w~ from your ~y~saved vehicles list~w~.");

            #region screen display
            veh.HazardLightsOn = true;
            Wait(200);
            veh.HazardLightsOn = false;
            Wait(250);
            veh.HazardLightsOn = true;
            Wait(200);
            veh.HazardLightsOn = false;
            #endregion

        }
        /// <summary>
        /// Adds a vehicle to the mainList with a new GUID
        /// </summary>
        /// <param name="veh"></param>
        void AddVehicle(Vehicle veh)
        {
            try
            {
                #region internal stuff
                Guid newID = Guid.NewGuid();
                IVVehicle newVeh = new IVVehicle();
                newVeh.ModelHash = veh.Model.Hash;
                newVeh.x = veh.Position.X;
                newVeh.y = veh.Position.Y;
                newVeh.z = veh.Position.Z;
                newVeh.ID = newID;
                veh.Metadata.GUID = newID;
                newVeh.h = veh.Heading;
                newVeh.Color = veh.Color.Index;
                newVeh.Color1 = veh.FeatureColor1.Index;
                newVeh.Color2 = veh.FeatureColor2.Index;
                mainList.Add(newVeh);
                newVeh.Fuel = -1.0f;
                #endregion internal stuff

                #region blip display
                Blip b = veh.AttachBlip();
                b.Color = BlipColor.White;
                b.Scale = .7f;
                b.Friendly = true;
                b.Display = BlipDisplay.ArrowAndMap;
                b.Name = Function.Call<string>("GET_DISPLAY_NAME_FROM_VEHICLE_MODEL", newVeh.ModelHash);
                blipList.Add(newVeh.ID, b);
                #endregion blip display

                DisplayHelp("Vehicle ~g~added~w~ to your ~y~saved vehicles list~w~.");

                #region screen display
                veh.HazardLightsOn = true;
                Wait(200);
                veh.HazardLightsOn = false;
                Wait(250);
                veh.HazardLightsOn = true;
                Wait(200);
                veh.HazardLightsOn = false;
                #endregion

                // Log the action
                DumpVehInfo(newVeh, "Added Vehicle");
            }
            catch (Exception crap) { Log("AddVehicle", crap.Message); }
        }
        /// <summary>
        /// Forces a vehicle to be placed if it is not already, and forces it as mission vehicle.
        /// </summary>
        /// <param name="veh"></param>
        void ForceVehicle(IVVehicle veh)
        {
            try
            {
                Vehicle scriptHandle;
                try
                {
                    scriptHandle = World.GetClosestVehicle(GetIVVehiclePosition(veh), 5.0f, new Model(veh.ModelHash));
                    if (scriptHandle.isAlive && scriptHandle.isDriveable)
                    {
                        scriptHandle.isRequiredForMission = true;
                        scriptHandle.Metadata.GUID = veh.ID;
                        // Object list wrongly defined in Fuel Script
                        // SendScriptCommand(FuelScriptGUID, "SetVehicleFuel", new ObjectCollection(new object[] { veh.Fuel, scriptHandle }));
                    }
                    else
                        RemoveVehicle(scriptHandle);
                    if (!blipList.ContainsKey(veh.ID))
                    {
                        #region blip display
                        Blip b = scriptHandle.AttachBlip();
                        b.Color = BlipColor.White;
                        b.Scale = .7f;
                        b.Friendly = true;
                        b.Display = BlipDisplay.ArrowAndMap;
                        b.Name = Function.Call<string>("GET_DISPLAY_NAME_FROM_VEHICLE_MODEL", veh.ModelHash);
                        blipList.Add(veh.ID, b);
                        DumpVehInfo(veh, "Added Blip for vehicle");
                        #endregion blip display
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        Function.Call("CLEAR_AREA_OF_CARS", veh.x, veh.y, veh.z, 1.0f);
                        // Re populate vehicle
                        Function.Call("REQUEST_MODEL", veh.ModelHash);
                        //Wait(250);
                        scriptHandle = World.CreateVehicle(new Model(veh.ModelHash), GetIVVehiclePosition(veh));
                        //Wait(250);
                        scriptHandle.Position = GetIVVehiclePosition(veh);
                        scriptHandle.Metadata.GUID = veh.ID;
                        scriptHandle.Heading = veh.h;
                        scriptHandle.Color = (ColorIndex)veh.Color;
                        scriptHandle.FeatureColor1 = (ColorIndex)veh.Color1;
                        scriptHandle.FeatureColor2 = (ColorIndex)veh.Color2;
                        scriptHandle.FreezePosition = true;
                        scriptHandle.PlaceOnGroundProperly();
                        scriptHandle.FreezePosition = false;
                        // Object list wrongly defined in Fuel Script
                        // SendScriptCommand(FuelScriptGUID, "SetVehicleFuel", new ObjectCollection(new object[] { veh.Fuel, scriptHandle}));

                        #region blip display
                        // Attach a new blip to the new handle
                        Blip b = scriptHandle.AttachBlip();
                        // Set blip properties
                        b.Color = BlipColor.White;
                        b.Scale = .7f;
                        b.Friendly = true;
                        b.Display = BlipDisplay.ArrowAndMap;
                        b.Name = Function.Call<string>("GET_DISPLAY_NAME_FROM_VEHICLE_MODEL", veh.ModelHash);
                        // Add or change the blip in the blipList
                        if (blipList.ContainsKey(veh.ID))
                        {
                            blipList[veh.ID].Delete();
                            blipList[veh.ID] = b;
                        }
                        else
                            blipList.Add(veh.ID, b);
                        #endregion blip display
                        DumpVehInfo(veh, "Spawned Vehicle");
                    }
                    catch (Exception crap) { Log("ForceVehicle-SpawnVehicle", crap.Message); }
                }
            }
            catch (Exception crap) { Log("ForceVehicle", crap.Message); }
        }
        /// <summary>
        /// Parses x, y and z values from IVVehicle structure into a Vector3 object.
        /// </summary>
        /// <param name="veh"></param>
        /// <returns></returns>
        Vector3 GetIVVehiclePosition(IVVehicle veh)
        {
            return new Vector3(veh.x, veh.y, veh.z - 1);
        }
        /// <summary>
        /// Displays a message at the bottom of the screen
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="time">Time in milliseconds</param>
        internal static void DisplayInfo(string message, int time)
        {
            Function.Call("PRINT_STRING_WITH_LITERAL_STRING_NOW", "STRING", message, time, true);
        }
        /// <summary>
        /// Displays a help message at the top left corner of the screen
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="keep">Keep the message untill a new one</param>
        internal static void DisplayHelp(string message, bool keep = false)
        {
            if (keep)
                AGame.PrintText(message);
            else
                AGame.PrintTextForever(message);
        }
        /// <summary>
        /// Clears the latest help message
        /// </summary>
        internal static void ClearHelp()
        {
            Function.Call("CLEAR_HELP");
        }
        #region Logging and Updating
        /// <summary>
        /// Append a new line to the log file
        /// </summary>
        /// <param name="methodName">The method that originated it</param>
        /// <param name="message">The exception's message</param>
        internal static void Log(string methodName, string message, bool printToConsole = false)
        {
            try
            {
                if (!string.IsNullOrEmpty(message))
                {
                    using (StreamWriter streamWriter = File.AppendText(Game.InstallFolder + "\\scripts\\" + mainScript.scriptName + ".log"))
                    {
                        streamWriter.WriteLine("[{0}] @ {1}: {2}", DateTime.Now.ToString("hh:mm:ss.fff"), methodName, message);
                        streamWriter.Flush();
                        streamWriter.Close();
                    }
#if DEBUG
                    Game.DisplayText(String.Format("[{0}] @ {1}: {2}", DateTime.Now.ToString("hh:mm:ss.fff"), methodName, message), 1500);
#endif
                }
            }
            catch
            {

            }
            finally
            {
                if (printToConsole)
                    Game.Console.Print(String.Format("{1}: {2}", methodName, message));
            }

        }
        /// <summary>
        /// Get OS name and SP
        /// </summary>
        /// <returns></returns>
        internal static string getOSInfo()
        {
            //Get Operating system information.
            OperatingSystem os = Environment.OSVersion;
            //Get version information about the os.
            Version vs = os.Version;

            //Variable to hold our return value
            string operatingSystem = "";

            if (os.Platform == PlatformID.Win32Windows)
            {
                //This is a pre-NT version of Windows
                switch (vs.Minor)
                {
                    case 0:
                        operatingSystem = "95";
                        break;
                    case 10:
                        if (vs.Revision.ToString() == "2222A")
                            operatingSystem = "98SE";
                        else
                            operatingSystem = "98";
                        break;
                    case 90:
                        operatingSystem = "Me";
                        break;
                    default:
                        break;
                }
            }
            else if (os.Platform == PlatformID.Win32NT)
            {
                switch (vs.Major)
                {
                    case 3:
                        operatingSystem = "NT 3.51";
                        break;
                    case 4:
                        operatingSystem = "NT 4.0";
                        break;
                    case 5:
                        if (vs.Minor == 0)
                            operatingSystem = "2000";
                        else
                            operatingSystem = "XP";
                        break;
                    case 6:
                        if (vs.Minor == 0)
                            operatingSystem = "Vista";
                        else
                            operatingSystem = "7";
                        break;
                    default:
                        break;
                }
            }
            //Make sure we actually got something in our OS check
            //We don't want to just return " Service Pack 2" or " 32-bit"
            //That information is useless without the OS version.
            if (operatingSystem != "")
            {
                //Got something.  Let's prepend "Windows" and get more info.
                operatingSystem = "Windows " + operatingSystem;
                //See if there's a service pack installed.
                if (os.ServicePack != "")
                {
                    //Append it to the OS name.  i.e. "Windows XP Service Pack 3"
                    operatingSystem += " " + os.ServicePack;
                }
                //Append the OS architecture.  i.e. "Windows XP Service Pack 3 32-bit"
                operatingSystem += " " + getOSArchitecture().ToString() + "-bit";
            }
            //Return the information we've gathered.
            return operatingSystem;
        }
        /// <summary>
        /// Get OS architecture
        /// </summary>
        /// <returns></returns>
        private static int getOSArchitecture()
        {
            string pa = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            return ((String.IsNullOrEmpty(pa) || String.Compare(pa, 0, "x86", 0, 3, true) == 0) ? 32 : 64);
        }
        #endregion Logging and Updating
        #endregion Methods
    }
}
