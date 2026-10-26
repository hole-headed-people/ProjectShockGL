using System.Numerics;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;

namespace ProjectShockGL;

public class Physics
{
    private readonly List<Collider> _colliders = [];
    private readonly HashSet<Collider> _registered = [];
    private readonly Dictionary<(Collider, Collider), Contact> _activePairs = [];
    
    private bool _isUpdating;
    
    private static Physics? _instance;

    private sealed class Contact(Collider a, Collider b, bool trigger)
    {
        public Collider A { get; } = a;
        public Collider B { get; } = b;
        public bool Trigger { get; } = trigger;
        public bool AEntered { get; set; }
        public bool BEntered { get; set; }
    }

    public static void Initialize()
    {
        _instance ??= new Physics();
    }

    public static void Register(Collider collider)
    {
        if (_instance != null && _instance._registered.Add(collider))
        {
            _instance._colliders.Add(collider);
        }
    }

    public static void Unregister(Collider collider)
    {
        if (_instance != null && _instance._registered.Remove(collider))
        {
            _instance._colliders.Remove(collider);
        }
    }

    public static void Update()
    {
        _instance?.UpdateInternal();
    }

    private static bool Alive(Collider collider)
    {
        return !collider.IsDestroyed && !collider.Parent.IsDestroyed;
    }

    private static bool Enabled(Collider collider)
    {
        return Alive(collider) && collider.IsEnabled && collider.Parent.IsActive;
    }

    private bool CanCollide(Collider a, Collider b)
    {
        return _registered.Contains(a) && _registered.Contains(b) && Enabled(a) && Enabled(b) &&
               a.Parent != b.Parent && !(a.Parent.IsStatic && b.Parent.IsStatic) &&
               (a.Parent.CollidesWith & b.Parent.Layer) != 0 && (b.Parent.CollidesWith & a.Parent.Layer) != 0;
    }

    private bool ContactValid(Contact contact)
    {
        return CanCollide(contact.A, contact.B) &&
               (contact.A.IsTrigger || contact.B.IsTrigger) == contact.Trigger;
    }

    private void EndContact((Collider, Collider) key, Contact contact)
    {
        if (!_activePairs.Remove(key))
        {
            return;
        }

        if (contact.AEntered && Alive(contact.A))
        {
            contact.A.NotifyExit(contact.B.Parent, contact.Trigger);
        }

        if (contact.BEntered && Alive(contact.B))
        {
            contact.B.NotifyExit(contact.A.Parent, contact.Trigger);
        }
    }

    private void UpdateInternal()
    {
        if (_isUpdating)
        {
            return;
        }
        
        _isUpdating = true;

        try
        {
            var snapshot = _colliders.ToArray();
            var current = new Dictionary<(Collider, Collider), bool>();

            for (var i = 0; i < snapshot.Length; i++)
            {
                var a = snapshot[i];

                if (!Enabled(a))
                {
                    continue;
                }

                var boundsA = a.GetWorldBounds();

                for (var j = i + 1; j < snapshot.Length; j++)
                {
                    var b = snapshot[j];

                    if (!CanCollide(a, b) || !PhysicsHelper.AabbOverlap(boundsA, b.GetWorldBounds()) ||
                        !a.Intersects(b))
                    {
                        continue;
                    }

                    current[(a, b)] = a.IsTrigger || b.IsTrigger;
                }
            }

            foreach (var (key, contact) in _activePairs.ToArray())
            {
                if (!current.TryGetValue(key, out var trigger) || trigger != contact.Trigger || !ContactValid(contact))
                {
                    EndContact(key, contact);
                }

            }

            foreach (var (key, trigger) in current)
            {
                if (_activePairs.ContainsKey(key))
                {
                    continue;
                }

                var contact = new Contact(key.Item1, key.Item2, trigger);

                if (!ContactValid(contact))
                {
                    continue;
                }

                _activePairs.Add(key, contact);

                contact.AEntered = true;
                contact.A.NotifyEnter(contact.B.Parent, trigger);

                if (ContactValid(contact))
                {
                    contact.BEntered = true;
                    contact.B.NotifyEnter(contact.A.Parent, trigger);
                }

                if (!ContactValid(contact))
                {
                    EndContact(key, contact);
                }
            }

            foreach (var (key, contact) in _activePairs.ToArray())
            {
                if (!ContactValid(contact)) EndContact(key, contact);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private static bool Matches(Collider collider, int layerMask, PhysicsQueryFilter filter)
    {
        return Enabled(collider) && (collider.Parent.Layer & layerMask) != 0 && filter.Allows(collider);
    }

    public static bool OverlapSphere(Vector3 center, float radius, int layerMask, out Collider? hit,
        PhysicsQueryFilter filter = default)
    {
        hit = null;

        if (_instance == null || !PhysicsHelper.IsFinite(center) || !ValidRadius(radius))
        {
            return false;
        }
        
        foreach (var collider in _instance._colliders)
        {
            if (!Matches(collider, layerMask, filter) ||
                !PhysicsHelper.IntersectsSphereCollider(center, radius, collider))
            {
                continue;
            }
            
            hit = collider;
            
            return true;
        }
        
        return false;
    }

    public static Collider[] OverlapSphereAll(Vector3 center, float radius, int layerMask,
        PhysicsQueryFilter filter = default)
    {
        if (_instance == null || !PhysicsHelper.IsFinite(center) || !ValidRadius(radius))
        {
            return [];
        }
        
        var result = new List<Collider>();
        
        foreach (var collider in _instance._colliders)
        {
            if (Matches(collider, layerMask, filter) &&
                PhysicsHelper.IntersectsSphereCollider(center, radius, collider))
            {
                result.Add(collider);
            }
        }
        
        return result.ToArray();
    }

    public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask,
        out RaycastHit hit, PhysicsQueryFilter filter = default)
    {
        hit = default;

        if (_instance == null || !PhysicsHelper.IsFinite(origin) || float.IsNaN(maxDistance) || maxDistance < 0 ||
            !PhysicsHelper.TryNormalize(direction, out var normalizedDir))
        {
            return false;
        }
        
        var closestDistance = float.PositiveInfinity;
        var closestNormal = Vector3.Zero;
        
        Collider? closest = null;
        
        foreach (var collider in _instance._colliders)
        {
            if (!Matches(collider, layerMask, filter))
            {
                continue;
            }

            if (!PhysicsHelper.RayIntersectsCollider(origin, normalizedDir, maxDistance, collider,
                    out var distance, out var normal) || distance >= closestDistance)
            {
                continue;
            }
            
            closestDistance = distance;
            closestNormal = normal;
            closest = collider;
        }

        if (closest == null)
        {
            return false;
        }
        
        hit = new RaycastHit(closest, closestDistance, origin + normalizedDir * closestDistance, closestNormal);
        
        return true;
    }

    public static bool OverlapCapsule(Vector3 a, Vector3 b, float radius, int layerMask, out Collider? hit,
        PhysicsQueryFilter filter = default)
    {
        hit = null;

        if (_instance == null || !PhysicsHelper.IsFinite(a) || !PhysicsHelper.IsFinite(b) || !ValidRadius(radius))
        {
            return false;
        }
        
        foreach (var collider in _instance._colliders)
        {
            if (!Matches(collider, layerMask, filter) ||
                !PhysicsHelper.CapsuleIntersectsCollider(a, b, radius, collider))
            {
                continue;
            }
            
            hit = collider;
            
            return true;
        }
        return false;
    }

    private static bool ValidRadius(float radius)
    {
        return float.IsFinite(radius) && radius >= 0;
    }
}

