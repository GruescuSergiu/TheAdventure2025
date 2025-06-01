using Silk.NET.SDL;
using System;
using TheAdventure.Models;

namespace TheAdventure.Models
{
    public class TemporaryGameObject : RenderableGameObject
    {
        public double Ttl { get; init; }
        public bool IsExpired => (DateTimeOffset.Now - _spawnTime).TotalSeconds >= Ttl;

        private DateTimeOffset _spawnTime;

        // 💣 Explosion Damage
        public int DamageAmount { get; set; } = 20;
        public int DamageRadius { get; set; } = 64; // pixels
        private bool hasExploded = false;

        public TemporaryGameObject(SpriteSheet spriteSheet, double ttl, (int X, int Y) position, double angle = 0.0, Point rotationCenter = new())
            : base(spriteSheet, position, angle, rotationCenter)
        {
            Ttl = ttl;
            _spawnTime = DateTimeOffset.Now;
        }

        public void TryExplode(PlayerObject player)
        {
            if (hasExploded || !IsExpired)
                return;

            var dx = player.Position.X - Position.X;
            var dy = player.Position.Y - Position.Y;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared <= DamageRadius * DamageRadius)
            {
                player.TakeDamage(DamageAmount);
            }

            hasExploded = true;
        }
    }
}
