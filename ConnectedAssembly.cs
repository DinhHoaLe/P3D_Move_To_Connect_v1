using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.ProcessPower.PnP3dObjects;

namespace PlanCad_MoveToConnect_ver1
{
    // A complete connected component, including joint entities that own weld/fastener
    // subparts. No Disconnect/Connect calls: existing joint identities are preserved.
    internal sealed class ConnectedAssembly
    {
        private readonly List<ObjectId> ids = new List<ObjectId>();
        private readonly List<PortState> ports = new List<PortState>();
        private readonly HashSet<ObjectId> members = new HashSet<ObjectId>();
        public int Count { get { return ids.Count; } }
        public bool Contains(ObjectId id) { return members.Contains(id); }

        public static ConnectedAssembly Capture(Transaction tr, ConnectionManager cm, ObjectId seed, Database db)
        {
            var result = new ConnectedAssembly();
            var found = GraphWalk.Component(seed, id => result.ReadNeighbours(tr, cm, id, db), 10000);
            result.ids.AddRange(found);
            foreach (var id in found) result.members.Add(id);
            return result;
        }

        private IEnumerable<ObjectId> ReadNeighbours(Transaction tr, ConnectionManager cm, ObjectId id, Database db)
        {
            if (id.IsNull || id.IsErased || id.Database != db)
                throw new InvalidOperationException("Cum co ket noi ngoai ban ve hoac doi tuong khong hop le.");
            var part = tr.GetObject(id, OpenMode.ForRead) as Part;
            if (!(part is Pipe) && !(part is InlineAsset) && !(part is Connector))
                throw new InvalidOperationException("Cum cham equipment/nozzle/support hoac doi tuong chua ho tro (handle " + id.Handle + "). Khong di chuyen mot phan cum.");
            var owner = tr.GetObject(part.OwnerId, OpenMode.ForRead) as BlockTableRecord;
            if (owner == null || !owner.IsLayout || owner.IsFromExternalReference)
                throw new InvalidOperationException("Cum co doi tuong trong block/xref.");
            if (((LayerTableRecord)tr.GetObject(part.LayerId, OpenMode.ForRead)).IsLocked || part.IsAnchored || part.PlacementLock)
                throw new InvalidOperationException("Doi tuong " + id.Handle + " bi khoa layer, anchored hoac PlacementLock.");

            var adjacent = new HashSet<ObjectId>();
            // GetConnections also exposes external edges that cannot be represented by
            // a local ObjectId. Never silently truncate a connected assembly there.
            var connections = cm.GetConnections(id);
            if (connections != null) foreach (Connection c in connections)
            {
                if (c.IsExternal || c.Pair1.IsUnresolved || c.Pair2.IsUnresolved)
                    throw new InvalidOperationException("Cum co ket noi xref/unresolved; khong the di chuyen toan bo.");
                adjacent.Add(c.ObjectId1 == id ? c.ObjectId2 : c.ObjectId1);
            }
            var names = new HashSet<string>();
            foreach (Port p in part.GetPorts(PortType.All))
            {
                if (string.IsNullOrEmpty(p.Name) || !names.Add(p.Name))
                    throw new InvalidOperationException("Port thieu ten hoac trung ten tai " + id.Handle);
                var state = new PortState { Id = id, Name = p.Name, Position = p.Position,
                    Direction = p.Direction, Symbolic = p.IsSymbolic };
                var pair = new Pair { ObjectId = id, Port = p };
                if (cm.IsConnected(pair))
                {
                    var other = cm.GetConnectedPairAt(pair);
                    if (other.IsUnresolved || other.ObjectId.IsNull || other.Port == null || other.Port.IsNull)
                        throw new InvalidOperationException("Khong doc duoc ket noi tai " + id.Handle + ":" + p.Name);
                    state.OtherId = other.ObjectId;
                    state.OtherName = other.Port.Name;
                    adjacent.Add(other.ObjectId);
                }
                ports.Add(state);
            }
            return adjacent;
        }

        public void Transform(Transaction tr, ConnectionManager cm, Matrix3d matrix)
        {
            Verify(tr, cm, Matrix3d.Identity, ObjectId.Null, null);
            // Open the complete set first, then apply exactly one rigid transform to
            // each entity. Connector-owned subparts travel with their parent.
            var parts = new List<Part>();
            foreach (var id in ids) parts.Add((Part)tr.GetObject(id, OpenMode.ForWrite));
            foreach (var part in parts) part.TransformBy(matrix);
            Verify(tr, cm, matrix, ObjectId.Null, null);
        }

        public void Verify(Transaction tr, ConnectionManager cm, Matrix3d matrix, ObjectId newConnectionId, string newConnectionPort)
        {
            foreach (var old in ports)
            {
                if (old.Id.IsErased) throw new InvalidOperationException("Doi tuong cu da bi xoa: " + old.Id.Handle);
                var part = (Part)tr.GetObject(old.Id, OpenMode.ForRead);
                Port current = null;
                foreach (Port p in part.GetPorts(PortType.All)) if (p.Name == old.Name) { current = p; break; }
                if (current == null) throw new InvalidOperationException("Mat port cu " + old.Name);
                if (!old.Symbolic && (current.Position.DistanceTo(old.Position.TransformBy(matrix)) > 1e-6 ||
                    (old.Direction.Length > 1e-10 && (current.Direction.Length < 1e-10 ||
                    current.Direction.GetNormal().GetAngleTo(old.Direction.TransformBy(matrix).GetNormal()) > 1e-7))))
                    throw new InvalidOperationException("Cum khong giu nguyen hinh hoc tai " + old.Id.Handle + ":" + old.Name);
                var pair = new Pair { ObjectId = old.Id, Port = current };
                bool connected = cm.IsConnected(pair);
                if (!old.OtherId.IsNull)
                {
                    if (!connected) throw new InvalidOperationException("Mat ket noi cu tai " + old.Id.Handle + ":" + old.Name);
                    var other = cm.GetConnectedPairAt(pair);
                    if (other.ObjectId != old.OtherId || other.Port.Name != old.OtherName)
                        throw new InvalidOperationException("Ket noi trong cum bi thay doi.");
                }
                else if (connected && !(old.Id == newConnectionId && old.Name == newConnectionPort))
                    throw new InvalidOperationException("Phat sinh ket noi ngoai y muon tai " + old.Id.Handle + ":" + old.Name);
            }
        }

        private sealed class PortState
        {
            public ObjectId Id, OtherId = ObjectId.Null;
            public string Name, OtherName;
            public Point3d Position;
            public Vector3d Direction;
            public bool Symbolic;
        }
    }
}
