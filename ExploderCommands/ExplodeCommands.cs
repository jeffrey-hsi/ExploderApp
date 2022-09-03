using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

[assembly: ExtensionApplication(null)]
[assembly: CommandClass(typeof(TangentExploder.ExplodeCommands))]

namespace TangentExploder
{
    public class ExplodeCommands
    {
        [CommandMethod("ExplodeTypes")]
        public static void ExplodeTypes()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var currDb = doc.Database;

            using (var lockDoc = doc.LockDocument())
            using (var transaction = currDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var blkTbl = (BlockTable)transaction.GetObject(
                        currDb.BlockTableId, OpenMode.ForRead);

                    var modelSpace = (BlockTableRecord)transaction.GetObject(
                        blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    if (GetTypes() is Regex types)
                    {
                        CheckInBlockTableRecord(transaction, modelSpace, types);
                    }
                }
                catch (Exception ex)
                {
                    var logPath =
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                        .Replace(@"\", @"/") + "/.tmp";
                    var info = Directory.CreateDirectory(logPath);
                    info.Attributes |= FileAttributes.Hidden;

                    using (var logFile = File.CreateText($"{logPath}/plugin.log"))
                    {
                        logFile.Write(ex.Message);
                    }
                }

                transaction.Commit();
            } // unlock
        }

        private static Regex GetTypes()
        {
            var typeFile =
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                .Replace(@"\", @"/") + "/Configuration/Types.txt";
            if (!File.Exists(typeFile))
            {
                return null;
            }

            var pattern = string.Join("|",
                File.ReadLines(typeFile).Select(line => line.Trim())
                                        .Where(line => line.Length > 0));
            return new Regex(pattern, RegexOptions.ECMAScript);
        }

        private static void CheckInBlockTableRecord(Transaction transaction, BlockTableRecord block, Regex regex)
        {
            foreach (var objId in block.Cast<ObjectId>()
                .Where(objId => objId.IsValid))
            {
                var entity = (Entity)transaction.GetObject(objId, OpenMode.ForRead,
                    openErased: false, forceOpenOnLockedLayer: true);

                RecursiveExplode(transaction, block, entity, regex);
            }
        }

        private static void RecursiveExplode(Transaction transaction, BlockTableRecord block, Entity entity, Regex regex)
        {
            Debug.Assert(entity.BlockId == block.Id);

            if (entity is BlockReference blockRef &&
                blockRef.BlockTableRecord is var subBlockId && subBlockId.IsValid)
            {
                var subBlock = (BlockTableRecord)transaction.GetObject(
                    subBlockId, OpenMode.ForRead);

                CheckInBlockTableRecord(transaction, subBlock, regex);
            }
            else if (regex.IsMatch(entity.GetRXClass().DxfName))
            {
                var objs = new DBObjectCollection();

                try
                {
                    entity.Explode(/*out*/ objs);
                }
                catch
                {
                    Application.DocumentManager.MdiActiveDocument.Editor
                        .WriteMessage($"{ entity.GetRXClass().DxfName } cannot be exploded\n");

                    throw;
                }

                block.UpgradeOpen();
                foreach (var obj in objs.Cast<Entity>())
                {
                    block.AppendEntity(obj);
                    transaction.AddNewlyCreatedDBObject(obj, true);

                    RecursiveExplode(transaction, block, obj, regex);
                }
                block.DowngradeOpen();

                entity.UpgradeOpen();
                entity.Erase();
            }
        }
    }
}
