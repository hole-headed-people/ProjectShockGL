using Object = ProjectShockGL.Core.Object;

namespace ProjectOxygenGL.Core;

public interface IDamageable
{
    void TakeDamage(Object caller, float damage, DamageInfo info = default);
}