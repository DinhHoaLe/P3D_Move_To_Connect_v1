using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.ProcessPower.PnP3dObjects;
using Autodesk.ProcessPower.PnP3dPipeRouting;
using Autodesk.ProcessPower.PnP3dTagUtil;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(PlanCad_MoveToConnect_ver1.MoveToConnectCommands))]
namespace PlanCad_MoveToConnect_ver1
{
    public class MoveToConnectCommands
    {
        [CommandMethod("PC_MTC", CommandFlags.Modal | CommandFlags.NoBlockEditor)]
        [CommandMethod("PC_MTC2", CommandFlags.Modal | CommandFlags.NoBlockEditor)]
        [CommandMethod("PC_MTC3", CommandFlags.Modal | CommandFlags.NoBlockEditor)]
        public void MoveToConnect()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            bool group = false, moved = false;
            string stage = "chon port";
            try
            {
                ed.WriteMessage("\nMove to Connect v3.1 - Chon dich dung yen truoc, cum di chuyen sau.");
                if (!db.TileMode) throw new InvalidOperationException("Hay chuyen sang Model tab.");
                int undo = Convert.ToInt32(Application.GetSystemVariable("UNDOCTL"));
                if ((undo & 1) == 0 || (undo & 2) != 0 || (undo & 8) != 0)
                    throw new InvalidOperationException("Can UNDO Control = All; khong co Undo group dang mo.");
                var cm = new ConnectionManager();
                var target = Pick(ed, db, cm, false);
                if (target == null) return;
                var source = Pick(ed, db, cm, true);
                if (source == null) return;
                ConnectedAssembly assembly;
                stage = "doc cum ket noi";
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    assembly = ConnectedAssembly.Capture(tr, cm, source.Id, db);
                    if (assembly.Contains(target.Id))
                        throw new InvalidOperationException("Dich nam trong cung cum nguon. Hay chon mot cum khac.");
                    tr.Commit();
                }
                ed.WriteMessage("\nCum nguon: {0} doi tuong (pipe, fitting, connector).", assembly.Count);
                stage = "kiem tra huong port";
                var from = source.Direction.GetNormal();
                var to = -target.Direction.GetNormal();
                double angle = from.GetAngleTo(to);
                if (angle > 1e-7)
                    throw new InvalidOperationException("Hai port khong doi huong, khong the noi bang tinh tien. Hay chon hai dau trong doi huong.");
                var transform = Matrix3d.Displacement(target.Position - source.Position);
                stage = "di chuyen cum";
                // All selection prompts finish before any model modification.
                ed.Command("_.UNDO", "_Begin");
                group = true;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    if (cm.IsConnected(PairAt(tr, target))) throw new InvalidOperationException("Port dich da duoc noi.");
                    assembly.Transform(tr, cm, transform);
                    var a = PairAt(tr, source).Port;
                    var b = PairAt(tr, target).Port;
                    if (a.Position.DistanceTo(b.Position) > 1e-6 || a.Direction.GetNormal().GetAngleTo(-b.Direction.GetNormal()) > 1e-7)
                        throw new InvalidOperationException("Khong the can chinh chinh xac hai port.");
                    tr.Commit();
                    moved = true;
                }
                // PLANTCONNECTJOINT merges collinear pipes in Plant 2027. Build and
                // append the configured connector explicitly to retain both pipe IDs.
                stage = "tao joint";
                using (var tr = db.TransactionManager.StartTransaction())
                using (var connector = new ConnectorWrapper())
                {
                    var a = PairAt(tr, source);
                    var b = PairAt(tr, target);
                    var jointNames = new StringCollection();
                    if (!connector.CanConnect(a, b, jointNames) || jointNames.Count == 0)
                        throw new InvalidOperationException("Project khong co joint tuong thich cho hai port.");
                    if (jointNames.Count != 1)
                        throw new InvalidOperationException("Co nhieu joint phu hop: " + string.Join(", ", ToArray(jointNames)) + ". Can chon joint ro rang truoc khi noi.");
                    connector.Connect(a, b, jointNames[0]);
                    connector.Append(a, b, PnP3dTagFormat.lineGroupIdFromObjId(target.Id));
                    tr.Commit();
                }
                stage = "kiem tra ket noi sau move";
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    assembly.Verify(tr, cm, transform, source.Id, source.Name);
                    var fixedPort = PairAt(tr, target).Port;
                    if (fixedPort.Position.DistanceTo(target.Position) > 1e-6 ||
                        fixedPort.Direction.GetNormal().GetAngleTo(target.Direction.GetNormal()) > 1e-7)
                        throw new InvalidOperationException("Plant da thay doi vi tri port dich. Hoan tac de giu cum dich co dinh.");
                    if (!Connected(tr, cm, PairAt(tr, source), PairAt(tr, target)))
                        throw new InvalidOperationException("Khong tao duoc joint. Kiem tra size, end type, spec va Joint Setup.");
                    tr.Commit();
                }
                ed.Command("_.UNDO", "_End");
                group = false;
                ed.Regen();
                ed.WriteMessage("\nDa move va connect cum {0} doi tuong; da kiem tra cac ket noi cu. Go U de hoan tac.", assembly.Count);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nPC_MTC3 [{0}]: {1}", stage, ex.Message);
                if (group)
                {
                    try
                    {
                        ed.Command("_.UNDO", "_End");
                        if (moved)
                        {
                            ed.Command("_.UNDO", 1);
                            ed.WriteMessage("\nDa hoan tac thao tac khong thanh cong.");
                        }
                    }
                    catch (System.Exception rollback)
                    {
                        ed.WriteMessage("\nKhong tu hoan tac duoc: " + rollback.Message + ". Kiem tra UNDO truoc khi tiep tuc.");
                    }
                }
            }
        }

        private static string[] ToArray(StringCollection values)
        {
            var result = new string[values.Count];
            values.CopyTo(result, 0);
            return result;
        }

        private static Endpoint Pick(Editor ed, Database db, ConnectionManager cm, bool source)
        {
            var opts = new PromptEntityOptions(source ? "\nBuoc 2 - Chon gan DAU TRONG cua cum pipe/fitting CAN DI CHUYEN: " : "\nBuoc 1 - Chon gan DAU TRONG cua pipe/fitting DUNG YEN: ");
            opts.SetRejectMessage("\nChi chon Plant 3D part.");
            opts.AddAllowedClass(typeof(Part), false);
            var result = ed.GetEntity(opts);
            if (result.Status != PromptStatus.OK) return null;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var part = (Part)tr.GetObject(result.ObjectId, OpenMode.ForRead);
                if (!(part is Pipe) && !(part is InlineAsset))
                    throw new InvalidOperationException("Ban nay chi ho tro Pipe va InlineAsset (fitting/valve).");
                if (source)
                {
                    if (((LayerTableRecord)tr.GetObject(part.LayerId, OpenMode.ForRead)).IsLocked)
                        throw new InvalidOperationException("Layer nguon dang khoa.");
                }
                var ports = new List<Endpoint>();
                foreach (Port p in part.GetPorts(PortType.Static))
                    if (!cm.IsConnected(new Pair { ObjectId = part.ObjectId, Port = p })) ports.Add(new Endpoint(part.ObjectId, p));
                if (ports.Count == 0) throw new InvalidOperationException("Khong co port trong.");
                var picked = result.PickedPoint.TransformBy(ed.CurrentUserCoordinateSystem);
                int nearest = 0;
                for (int i = 0; i < ports.Count; i++)
                {
                    if (ports[i].Position.DistanceTo(picked) < ports[nearest].Position.DistanceTo(picked)) nearest = i;
                }
                var selected = ports[nearest];
                ed.WriteMessage("\nDa chon port {0}, WCS ({1:F3}, {2:F3}, {3:F3}).", selected.Name,
                    selected.Position.X, selected.Position.Y, selected.Position.Z);
                tr.Commit();
                return selected;
            }
        }

        private static Pair PairAt(Transaction tr, Endpoint e)
        {
            var part = (Part)tr.GetObject(e.Id, OpenMode.ForRead);
            foreach (Port p in part.GetPorts(PortType.Static))
                if (p.Name == e.Name) return new Pair { ObjectId = e.Id, Port = p };
            throw new InvalidOperationException("Khong tim thay port " + e.Name);
        }

        private static bool Connected(Transaction tr, ConnectionManager cm, Pair source, Pair target)
        {
            // Follow only connectors, never a different pipe run; match the exact port.
            var pending = new Queue<Pair>();
            var visited = new HashSet<ObjectId>();
            pending.Enqueue(source);
            while (pending.Count > 0 && visited.Count < 32)
            {
                var p = pending.Dequeue();
                if (!cm.IsConnected(p)) continue;
                var next = cm.GetConnectedPairAt(p);
                if (next.ObjectId == target.ObjectId && next.Port.Name == target.Port.Name) return true;
                if (next.ObjectId.IsNull || !visited.Add(next.ObjectId)) continue;
                var connector = tr.GetObject(next.ObjectId, OpenMode.ForRead) as Connector;
                if (connector == null) continue;
                foreach (Port port in connector.GetPorts(PortType.All))
                    if (port.Name != next.Port.Name) pending.Enqueue(new Pair { ObjectId = next.ObjectId, Port = port });
            }
            return false;
        }

        private sealed class Endpoint
        {
            public readonly ObjectId Id;
            public readonly string Name;
            public readonly Point3d Position;
            public readonly Vector3d Direction;
            public Endpoint(ObjectId id, Port p)
            {
                Id = id; Name = p.Name; Position = p.Position; Direction = p.Direction;
                if (string.IsNullOrEmpty(Name) || Direction.Length < 1e-10)
                    throw new InvalidOperationException("Port thieu ten hoac huong hop le.");
            }
        }
    }
}
