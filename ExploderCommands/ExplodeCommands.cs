using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TZExploder
{
    public class ExplodeCommands
    {
        [Autodesk.AutoCAD.Runtime.CommandMethod("HELLO")]
        public void HelloCommand()
        {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nHello World!\n");
        }
    }
}
