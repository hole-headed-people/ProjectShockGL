using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectOxygenGL.Core;

public class DamageableEntity : Entity,
    IDamageable,
    IKillable
{
    public event Action<float>? CurrentHealthChanged; 
    public event Action<Object, float, DamageInfo>? DamageReceived; 
    
    private const float DefaultMaxHealth = 100f;

    private float _maxHealth = DefaultMaxHealth;
    
    private bool _isHealthInitialized;
    private bool _isDead;

    public float CurrentHealth { get; private set; } = DefaultMaxHealth;

    [Editable]
    public float MaxHealth
    {
        get => _maxHealth;
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                return;
            }

            _maxHealth = value;
            
            SetCurrentHealth(_isHealthInitialized || _isDead ? MathF.Min(CurrentHealth, value) : value);
        }
    }

    public override void Start()
    {
        _isHealthInitialized = true;
        base.Start();
    }
    
    protected virtual void OnHit(Object caller, float damage, DamageInfo info){}
    protected virtual void OnTakeDamage(Object caller, float damage, DamageInfo info = default){}
    protected virtual void OnDied(Object? caller, Object? instigator = null){}
    
    public void TakeDamage(Object caller, float damage, DamageInfo info = default)
    {
        if (IsDestroyed || !float.IsFinite(damage) || damage <= 0f)
        {
            return;
        }

        OnHit(caller, damage, info);

        if (_isDead || IsDestroyed)
        {
            return;
        }
        
        _isHealthInitialized = true;
        
        var health = MathF.Max(0f, CurrentHealth - damage);
        var died = health == 0f;
        
        _isDead = died;
        
        SetCurrentHealth(health);

        if (died || (!_isDead && !IsDestroyed))
        {
            DamageReceived?.Invoke(caller, damage, info);
            OnTakeDamage(caller, damage, info);
        }

        if (died)
        {
            OnDied(caller, info.Instigator);
        }
    }
    
    public override void Kill()
    {
        try
        {
            Kill(null);
        }
        finally
        {
            base.Kill();
        }
    }

    public void Kill(Object? caller, Object? instigator = null)
    {
        if (_isDead || IsDestroyed)
        {
            return;
        }
        
        _isDead = true;

        SetCurrentHealth(0f);
        OnDied(caller, instigator);
    }

    protected void SetCurrentHealth(float health)
    {
        if (!float.IsFinite(health))
        {
            return;
        }

        health = Math.Clamp(health, 0f, MaxHealth);
        
        if (CurrentHealth == health)
        {
            return;
        }

        CurrentHealth = health;
        CurrentHealthChanged?.Invoke(health);
    }

    public bool IsDead()
    {
        return _isDead;
    }
}
