﻿using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

[assembly: ExtensionApplication(null)]
[assembly: CommandClass(typeof(ExplodeCommand))]

class ExplodeCommand
{
    private static readonly string LOG_PATH =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        .Replace(@"\", @"/") + "/.tmp";

    // lifetime as the command
    private static Transaction transaction;

    [CommandMethod("ExplodeTypes")]
    public static void ExplodeTypes()
    {
        var info = Directory.CreateDirectory(LOG_PATH);
        info.Attributes |= FileAttributes.Hidden;

        var doc = Application.DocumentManager.MdiActiveDocument;
        var currDb = doc.Database;

        using (var lockDoc = doc.LockDocument())
        using (transaction = currDb.TransactionManager.StartTransaction())
        {
            try
            {
                var blkTbl = (BlockTable)transaction.GetObject(
                    currDb.BlockTableId, OpenMode.ForRead);

                var modelSpace = (BlockTableRecord)transaction.GetObject(
                    blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                if (GetTypes() is Regex types)
                {
                    CheckInBlockTableRecord(modelSpace, types);

                    // if tangent's entities are exploded, registered application "TCH" need to be erased
                    if (types.IsMatch("TCH_.*"))
                    {
                        var regApps = (RegAppTable)transaction.GetObject(
                            currDb.RegAppTableId, OpenMode.ForRead);
                        foreach (var regApp in regApps.Cast<ObjectId>()
                            .Where(id => id.IsValid)
                            .Select(id => (RegAppTableRecord)transaction.GetObject(id, OpenMode.ForWrite)))
                        {
                            if (regApp.Name == "_TCH")
                            {
                                regApp.Erase();
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                using (var logFile = File.CreateText($"{LOG_PATH}/plugin.log"))
                {
                    logFile.Write(ex.Message);
                }
            }
            catch
            {
                using (var logFile = File.CreateText($"{LOG_PATH}/plugin.log"))
                {
                    logFile.Write("Unknown error (not from AutoCAD)");
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

    private static void CheckInBlockTableRecord(BlockTableRecord block, Regex regex)
    {
        foreach (var entity in block.Cast<ObjectId>()
            .Where(objId => objId.IsValid)
            .Select(id => (Entity)transaction.GetObject(id, OpenMode.ForRead,
                openErased: false, forceOpenOnLockedLayer: true)))
        {
            RecursiveExplode(block, entity, regex);
        }
    }

    private static void RecursiveExplode(BlockTableRecord block, Entity entity, Regex regex)
    {
        Debug.Assert(entity.BlockId == block.Id);

        if (entity is BlockReference blockRef &&
            blockRef.BlockTableRecord is var subBlockId && subBlockId.IsValid)
        {
            var subBlock = (BlockTableRecord)transaction.GetObject(
                subBlockId, OpenMode.ForRead);

            CheckInBlockTableRecord(subBlock, regex);
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

                RecursiveExplode(block, obj, regex);
            }
            block.DowngradeOpen();

            entity.UpgradeOpen();
            entity.Erase();
        }
    }
}
