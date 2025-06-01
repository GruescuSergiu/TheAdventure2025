using System.Text.Json;
using Silk.NET.Maths;
using TheAdventure.Models;
using TheAdventure.Models.Data;

namespace TheAdventure;


    
    public class Engine
{
    private readonly GameRenderer _renderer;
    private int? _youDiedTextureId = null;
    private TextureData _youDiedTextureData;
    private readonly Input _input;

    private readonly Dictionary<int, GameObject> _gameObjects = new();
    private readonly Dictionary<string, TileSet> _loadedTileSets = new();
    private readonly Dictionary<int, Tile> _tileIdMap = new();

    private Level _currentLevel = new();
    private PlayerObject? _player;

    private DateTimeOffset _lastUpdate = DateTimeOffset.Now;
    private DateTimeOffset _lastDamageTime = DateTimeOffset.MinValue;
    private DateTimeOffset _startTime = DateTimeOffset.Now;
    private double _timeSinceLastBomb = 0;

    public Engine(GameRenderer renderer, Input input)
    {
        _renderer = renderer;
        _input = input;

        _input.OnMouseClick += (_, coords) => AddBomb(coords.x, coords.y);
    }

    public void SetupWorld()
    {
        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);

        var levelContent = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));
        var level = JsonSerializer.Deserialize<Level>(levelContent);
        if (level == null)
        {
            throw new Exception("Failed to load level");
        }

        foreach (var tileSetRef in level.TileSets)
        {
            var tileSetContent = File.ReadAllText(Path.Combine("Assets", tileSetRef.Source));
            var tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent);
            if (tileSet == null)
            {
                throw new Exception("Failed to load tile set");
            }

            foreach (var tile in tileSet.Tiles)
            {
                tile.TextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                _tileIdMap.Add(tile.Id!.Value, tile);
            }

            _loadedTileSets.Add(tileSet.Name, tileSet);
        }

        if (level.Width == null || level.Height == null)
        {
            throw new Exception("Invalid level dimensions");
        }

        if (level.TileWidth == null || level.TileHeight == null)
        {
            throw new Exception("Invalid tile dimensions");
        }

        _renderer.SetWorldBounds(new Rectangle<int>(0, 0, level.Width.Value * level.TileWidth.Value,
            level.Height.Value * level.TileHeight.Value));

        _currentLevel = level;
        _startTime = DateTimeOffset.Now;
    }

    public void ProcessFrame()
    {
        var currentTime = DateTimeOffset.Now;
        var msSinceLastFrame = (currentTime - _lastUpdate).TotalMilliseconds;
        _lastUpdate = currentTime;

        double up = _input.IsUpPressed() ? 1.0 : 0.0;
        double down = _input.IsDownPressed() ? 1.0 : 0.0;
        double left = _input.IsLeftPressed() ? 1.0 : 0.0;
        double right = _input.IsRightPressed() ? 1.0 : 0.0;

        _player?.UpdatePosition(up, down, left, right, 48, 48, msSinceLastFrame);

        if (_input.IsSpacePressed())
        {
            if ((currentTime - _lastDamageTime).TotalMilliseconds > 500)
            {
                _player?.TakeDamage(10);
                _lastDamageTime = currentTime;
            }
        }

        // 🧨 Bomb spawning logic
        _timeSinceLastBomb += msSinceLastFrame;
        double totalSeconds = (currentTime - _startTime).TotalSeconds;

        if (_timeSinceLastBomb >= 3000)
        {
            _timeSinceLastBomb = 0;

            int bombsToSpawn = 1;
            if (totalSeconds >= 60)
                bombsToSpawn = 3;
            else if (totalSeconds >= 30)
                bombsToSpawn = 2;

            for (int i = 0; i < bombsToSpawn; i++)
                SpawnBombNearPlayer();
        }
    }

    public void RenderFrame()
    {
        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        var playerPosition = _player!.Position;
        _renderer.CameraLookAt(playerPosition.X, playerPosition.Y);

        RenderTerrain();
        RenderAllObjects();

        
        if (_player!.IsDead)
        {
            if (_youDiedTextureId == null)
            {
                _youDiedTextureId = _renderer.LoadTexture("Assets/youdied.png", out _youDiedTextureData);
            }

            var windowSize = _renderer.GetWindowSize();
            var dstRect = new Silk.NET.Maths.Rectangle<int>(
                (windowSize.X - _youDiedTextureData.Width) / 2,
                (windowSize.Y - _youDiedTextureData.Height) / 2,
                _youDiedTextureData.Width,
                _youDiedTextureData.Height
            );

            _renderer.RenderTexture(_youDiedTextureId.Value,
                new Silk.NET.Maths.Rectangle<int>(0, 0, _youDiedTextureData.Width, _youDiedTextureData.Height),
                dstRect);
            _renderer.PresentFrame();
            return;
        }
_renderer.RenderFrame(_player);
    }

    public void RenderAllObjects()
    {
        var toRemove = new List<int>();
        foreach (var gameObject in GetRenderables())
        {
            gameObject.Render(_renderer);

            if (gameObject is TemporaryGameObject tempGameObject)
            {
                if (tempGameObject.IsExpired)
                {
                    tempGameObject.TryExplode(_player!);
                    toRemove.Add(tempGameObject.Id);
                }
            }
        }

        foreach (var id in toRemove)
        {
            _gameObjects.Remove(id);
        }

        _player?.Render(_renderer);
    }

    public void RenderTerrain()
    {
        foreach (var currentLayer in _currentLevel.Layers)
        {
            for (int i = 0; i < _currentLevel.Width; ++i)
            {
                for (int j = 0; j < _currentLevel.Height; ++j)
                {
                    int? dataIndex = j * currentLayer.Width + i;
                    if (dataIndex == null)
                    {
                        continue;
                    }

                    var currentTileId = currentLayer.Data[dataIndex.Value] - 1;
                    if (currentTileId == null)
                    {
                        continue;
                    }

                    var currentTile = _tileIdMap[currentTileId.Value];

                    var tileWidth = currentTile.ImageWidth ?? 0;
                    var tileHeight = currentTile.ImageHeight ?? 0;

                    var sourceRect = new Rectangle<int>(0, 0, tileWidth, tileHeight);
                    var destRect = new Rectangle<int>(i * tileWidth, j * tileHeight, tileWidth, tileHeight);
                    _renderer.RenderTexture(currentTile.TextureId, sourceRect, destRect);
                }
            }
        }
    }

    public IEnumerable<RenderableGameObject> GetRenderables()
    {
        foreach (var gameObject in _gameObjects.Values)
        {
            if (gameObject is RenderableGameObject renderableGameObject)
            {
                yield return renderableGameObject;
            }
        }
    }

    private void AddBomb(int screenX, int screenY)
    {
        var worldCoords = _renderer.ToWorldCoordinates(screenX, screenY);
        CreateBomb(worldCoords.X, worldCoords.Y);
    }

    private void SpawnBombNearPlayer()
    {
        if (_player == null) return;

        Random rng = new();
        float offsetX = rng.Next(-100, 101);
        float offsetY = rng.Next(-100, 101);
        float x = _player.Position.X + offsetX;
        float y = _player.Position.Y + offsetY;

        CreateBomb(x, y);
    }

    private void CreateBomb(float x, float y)
{
    SpriteSheet spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
    spriteSheet.ActivateAnimation("Explode");

    TemporaryGameObject bomb = new(spriteSheet, 2.1, ((int)x, (int)y))
    {
        DamageAmount = 20,
        DamageRadius = 64
    };

    _gameObjects.Add(bomb.Id, bomb);
}

}
