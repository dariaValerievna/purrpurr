using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Security.Cryptography.X509Certificates;

namespace purrpurrPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    public class Start : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UserWindowControl userWindowControl = new UserWindowControl();
            userWindowControl.Show();
            return Result.Succeeded;
        }
    }

    internal class PluginEngine
    {
        public int RoomCount { get; }
        public bool IsSeparateBathrooms { get; }
        public bool IsWardrobe { get; } 
        public bool IsCombinedKitchen { get; }
        public bool IsLoggia { get; }
        public int HouseClass { get; }
        public PluginEngine(int RC, bool ISB, bool IW, bool ICK, bool IL, int HC)
        { 
            RoomCount = RC;
            IsSeparateBathrooms = ISB;
            IsWardrobe = IW;
            IsCombinedKitchen = ICK;
            IsLoggia = IL;
            HouseClass = HC;
        }
        public void Run()
        {
            
        }
    }
}
