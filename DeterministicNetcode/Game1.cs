using System;
using System.Collections.Generic;
using System.Net.Sockets;
using DeterministicNetcode.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DeterministicNetcode;

/*
 * Determinism:
 *  Don't use:
 *      - System time, delta time, etc.
 *      - Floats and doubles (.NET floats aren't guaranteed to be deterministic)
 *      - Keyboard, mouse, etc. Relevant inputs will be passed to deterministic code.
 *      - Anything other than current state and relevant inputs!
 */

public class Game1 : Game
{
    private enum State
    {
        MainMenu,
        Lobby,
        // Unlike the starting game net state, this state exists just to give visual feedback to the host
        // when they try to start the game. Non-hosts will stay in the lobby while the net peer coordinates
        // starting the game behind the scenes.
        StartingGame,
        InGame
    }

    private const int StepsPerSecond = 600;
    private const float StepTime = 1f / StepsPerSecond;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _smileyTexture, _wallTexture;

    private double _stepTimer;
    private int _stepCount;

    private readonly List<InputState> _bufferedLocalInputs = new();
    private InputState[] _inputStates;
    private DeterministicGame _deterministicGame;
    private State _state = State.MainMenu;
    private INetPeer _netPeer;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        IsFixedTimeStep = false;
        _graphics.SynchronizeWithVerticalRetrace = false;

        InactiveSleepTime = TimeSpan.Zero;
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _smileyTexture = Content.Load<Texture2D>("Smiley");
        _wallTexture = Content.Load<Texture2D>("Wall");
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboardState = Keyboard.GetState();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            keyboardState.IsKeyDown(Keys.Escape))
            Exit();

        Window.Title = $"{Math.Ceiling(1f / gameTime.ElapsedGameTime.TotalSeconds)} fps, {_stepCount} steps";

        switch (_state)
        {
            case State.MainMenu:
                UpdateMainMenu(keyboardState);
                break;
            case State.Lobby:
                UpdateLobby(keyboardState);
                break;
            case State.StartingGame:
                UpdateStartingGame();
                break;
            case State.InGame:
                UpdateInGame(gameTime, keyboardState);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        base.Update(gameTime);
    }

    private void UpdateMainMenu(KeyboardState keyboardState)
    {
        if (keyboardState.IsKeyDown(Keys.D1))
        {
            // Start the server.
            _netPeer = new NetHost();
            _state = State.Lobby;
            Console.WriteLine($"Started server with port: {_netPeer.Messenger.Port}");
            Console.WriteLine("Press SPACE when you're ready to start the game.");
            return;
        }

        if (keyboardState.IsKeyDown(Keys.D2))
        {
            // Start the client.
            while (true)
            {
                Console.Write("Host IP: ");
                var hostIp = Console.ReadLine();

                Console.Write("Host port: ");
                var hostPortString = Console.ReadLine();
                if (!int.TryParse(hostPortString, out var hostPort))
                {
                    Console.WriteLine("Invalid port, please try again!");
                    continue;
                }

                try
                {
                    _netPeer = new NetPeer(new NetMessenger.Address { Ip = hostIp, Port = hostPort });
                }
                catch (Exception exception) when (exception is ArgumentOutOfRangeException or SocketException)
                {
                    Console.WriteLine("Failed to connect, did you use the right IP and port?");
                    continue;
                }

                break;
            }

            _state = State.Lobby;
            return;
        }
    }

    private void UpdateLobby(KeyboardState keyboardState)
    {
        _netPeer.Poll(_stepCount);

        if (_netPeer.State == NetState.InGame)
        {
            _state = State.InGame;
            return;
        }

        if (_netPeer.IsHost && keyboardState.IsKeyDown(Keys.Space))
        {
            var netHost = (NetHost)_netPeer;
            netHost.BeginStartingGame();
            _state = State.StartingGame;
        }
    }

    private void UpdateStartingGame()
    {
        _netPeer.Poll(_stepCount);

        if (_netPeer.State == NetState.InGame)
        {
            _state = State.InGame;
        }
    }

    private void UpdateInGame(GameTime gameTime, KeyboardState keyboardState)
    {
        if (_deterministicGame is null)
        {
            var playerCount = _netPeer.Messenger.PeerCount + 1;
            _deterministicGame = new DeterministicGame(playerCount);
            _inputStates = new InputState[playerCount];
        }

        _netPeer.Poll(_stepCount);
        _stepTimer += gameTime.ElapsedGameTime.TotalSeconds;

        if (_bufferedLocalInputs.Count == 0 || _bufferedLocalInputs[^1].StepIndex < _stepCount)
        {
            var axisX = 0;
            var axisY = 0;

            if (keyboardState.IsKeyDown(Keys.Left)) axisX -= 1;
            if (keyboardState.IsKeyDown(Keys.Right)) axisX += 1;
            if (keyboardState.IsKeyDown(Keys.Up)) axisY -= 1;
            if (keyboardState.IsKeyDown(Keys.Down)) axisY += 1;

            _bufferedLocalInputs.Add(new InputState { AxisX = axisX, AxisY = axisY, StepIndex = _stepCount});
        }

        while (_bufferedLocalInputs.Count > InputState.SavedInputStateCount) _bufferedLocalInputs.RemoveAt(0);

        _netPeer.Messenger.SendInputState(_bufferedLocalInputs);

        if (_stepTimer > StepTime && _netPeer.Messenger.HasAllPeerInputStates())
        {
            for (var i = 0; i < _netPeer.Messenger.PeerCount; i++)
            {
                _inputStates[i] = _netPeer.Messenger.PeerInputStates[i]!.Value;
            }

            _inputStates[_netPeer.Messenger.PeerCount] = _bufferedLocalInputs[^1];

            _stepTimer -= StepTime;
            _deterministicGame.DeterministicStep(_inputStates);

            _netPeer.Messenger.ClearInputStates();

            _stepCount++;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        switch (_state)
        {
            case State.MainMenu:
                DrawMainMenu();
                break;
            case State.Lobby:
                DrawLobby();
                break;
            case State.StartingGame:
                DrawStartingGame();
                break;
            case State.InGame:
                DrawInGame();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        base.Draw(gameTime);
    }

    private void DrawMainMenu()
    {

    }

    private void DrawLobby()
    {

    }

    private void DrawStartingGame()
    {

    }

    private void DrawInGame()
    {
        if (_deterministicGame is null) return;

        _spriteBatch.Begin();
        foreach (var playerPosition in _deterministicGame.PlayerPositions)
        {
            _spriteBatch.Draw(_smileyTexture, playerPosition.ToVector2(), Color.White);
        }
        _spriteBatch.Draw(_wallTexture, _deterministicGame.WallPosition.ToVector2(), Color.White);
        _spriteBatch.End();
    }

    protected override void Dispose(bool disposing)
    {
        _netPeer?.Dispose();

        base.Dispose(disposing);
    }
}