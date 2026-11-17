using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.ProcessPower.PnP3dObjects;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(PlanCad_MoveToConnect_ver1.IntegrationDiagnostics))]
namespace PlanCad_MoveToConnect_ver1
{
    public class IntegrationDiagnostics
    {
        [CommandMethod("PC_MTC_AUDIT")]
        public void Audit()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var items = new List<object>();
            var cm = new ConnectionManager();
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in model)
                {
                    var part = tr.GetObject(id, OpenMode.ForRead) as Part;
                    if (part == null) continue;
                    var ports = new List<object>();
                    foreach (Port p in part.GetPorts(PortType.All))
                    {
                        var pair = new Pair { ObjectId = id, Port = p };
                        var connected = cm.IsConnected(pair);
                        var other = connected ? cm.GetConnectedPairAt(pair) : null;
                        ports.Add(new { p.Name, Position = new[] { p.Position.X, p.Position.Y, p.Position.Z },
                            Direction = new[] { p.Direction.X, p.Direction.Y, p.Direction.Z }, connected,
                            Other = other == null || other.ObjectId.IsNull ? null : other.ObjectId.Handle.ToString(),
                            OtherPort = other == null ? null : other.Port.Name });
                    }
                    items.Add(new { Handle = id.Handle.ToString(), Type = part.GetType().Name, ports });
                }
                tr.Commit();
            }
            string folder = Path.Combine(Path.GetDirectoryName(typeof(IntegrationDiagnostics).Assembly.Location), "audit");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "audit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(new { Drawing = doc.Name, items }, new JsonSerializerOptions { WriteIndented = true }));
            doc.Editor.WriteMessage("\nAUDIT: {0} parts. {1}", items.Count, path);
        }
    }
}
