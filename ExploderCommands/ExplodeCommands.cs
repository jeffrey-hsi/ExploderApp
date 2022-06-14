using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

[assembly: ExtensionApplication(null)]
[assembly: CommandClass(typeof(TangentExploder.ExplodeCommands))]

namespace TangentExploder
{
    public class ExplodeCommands
    {
        [CommandMethod("ExplodeTypes")]
        static public void ExplodeTypes()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var currDb = doc.Database;

            using (var lockDoc = doc.LockDocument())
            using (var transaction = currDb.TransactionManager.StartTransaction())
            {
                var blkTbl = (BlockTable)transaction.GetObject(
                    currDb.BlockTableId, OpenMode.ForRead);

                var blkTblRec = (BlockTableRecord)transaction.GetObject(
                    blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                CheckInBlockTableRecord(transaction, blkTblRec, GetTypes());

                transaction.Commit();
            } // unlock
        }

        static private Regex GetTypes()
        {
            var currPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                .Replace(@"\", @"/");
            var pattern = string.Empty;
            foreach (var line in File.ReadLines(currPath + "/Types.txt"))
            {
                if (line.Trim().Length > 0)
                {
                    pattern += line;
                    pattern += '|';
                }
            }
            if (pattern.Length > 0)
            {
                pattern = pattern.TrimEnd('|');
            }
            return new Regex(pattern, RegexOptions.ECMAScript);
        }

        static private void CheckInBlockTableRecord(Transaction transaction, BlockTableRecord blkTblRec, Regex regex)
        {
            foreach (var objId in blkTblRec)
            {
                var dbObject = (Entity)transaction.GetObject(objId, OpenMode.ForWrite);
                Debug.Assert(dbObject != null);

                RecursiveExplode(transaction, blkTblRec, dbObject, regex);
            }
        }

        static private void RecursiveExplode(Transaction transaction, BlockTableRecord blkTblRec, Entity dbObject, Regex regex)
        {
            if (dbObject is BlockReference blockRef)
            {
                var subBlkTblRec = (BlockTableRecord)transaction.GetObject(
                    blockRef.BlockTableRecord, OpenMode.ForRead);

                CheckInBlockTableRecord(transaction, subBlkTblRec, regex);
            }
            else if (regex.IsMatch(dbObject.GetRXClass().DxfName))
            {
                var objs = new DBObjectCollection();

                try
                {
                    dbObject.Explode(/*out*/ objs);
                }
                catch
                {
                    Application.DocumentManager.MdiActiveDocument.Editor
                        .WriteMessage($"{ dbObject.GetRXClass().DxfName } cannot be exploded\n");
                }

                foreach (Entity obj in objs)
                {
                    blkTblRec.UpgradeOpen();
                    blkTblRec.AppendEntity(obj);
                    transaction.AddNewlyCreatedDBObject(obj, true);
                    blkTblRec.DowngradeOpen();

                    RecursiveExplode(transaction, blkTblRec, obj, regex);
                }
                dbObject.UpgradeOpen();
                dbObject.Erase();
            }
        }
    }
}
