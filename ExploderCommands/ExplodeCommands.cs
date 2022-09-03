﻿using Autodesk.AutoCAD.ApplicationServices;
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
                var blkTbl = (BlockTable)transaction.GetObject(
                    currDb.BlockTableId, OpenMode.ForRead);

                var modelSpace = (BlockTableRecord)transaction.GetObject(
                    blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                if (GetTypes() is Regex types)
                {
                    CheckInBlockTableRecord(transaction, modelSpace, types);
                }

                transaction.Commit();
            } // unlock
        }

        private static Regex GetTypes()
        {
            var currPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                .Replace(@"\", @"/");
            var typeFile = currPath + "/Types.txt";
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
            foreach (var objId in block)
            {
                var entity = (Entity)transaction.GetObject(objId, OpenMode.ForRead);

                RecursiveExplode(transaction, block, entity, regex);
            }
        }

        private static void RecursiveExplode(Transaction transaction, BlockTableRecord block, Entity entity, Regex regex)
        {
            Debug.Assert(entity.BlockId == block.Id);

            if (entity is BlockReference blockRef)
            {
                var subBlock = (BlockTableRecord)transaction.GetObject(
                    blockRef.BlockTableRecord, OpenMode.ForRead);

                CheckInBlockTableRecord(transaction, subBlock, regex);
            }
            else if (regex.IsMatch(entity.GetRXClass().DxfName))
            {
                var objs = new DBObjectCollection();

                entity.Explode(/*out*/ objs);

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
