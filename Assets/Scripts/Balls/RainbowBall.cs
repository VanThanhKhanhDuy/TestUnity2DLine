using UnityEngine;
public class RainbowBall : Ball
{
    public override void SetBallColor(Color _color, int _colorValue)
    {
        return;
    }
    public override void OnExplode()
    {
        var explode = Instantiate(explodeFX, transform.position, Quaternion.identity);
        Destroy(explode, 1f);
    }
}
