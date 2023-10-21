using System;
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
        InGame
    }

    private const int StepsPerSecond = 60;

    private const float StepTime = 1f / StepsPerSecond;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _smileyTexture, _wallTexture;

    private double _stepTimer;

    private InputState _bufferedLocalInput;
    private readonly DeterministicGame _deterministicGame = new();
    private State _state = State.MainMenu;
    private INetPeer _netPeer;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
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

        switch (_state)
        {
            case State.MainMenu:
                UpdateMainMenu(keyboardState);
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
            _state = State.InGame;
            Console.WriteLine($"Started server with port: {_netPeer.Port}");
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

            _state = State.InGame;
            return;
        }
    }

    private void UpdateInGame(GameTime gameTime, KeyboardState keyboardState)
    {
        _netPeer.Poll();

        if (keyboardState.IsKeyDown(Keys.M))
        {
            // _net.SendMessageToAll($"Hello world @ {gameTime.TotalGameTime.Seconds}");
        }

        _stepTimer += gameTime.ElapsedGameTime.TotalSeconds;

        var axisX = 0;
        var axisY = 0;

        if (keyboardState.IsKeyDown(Keys.Left)) axisX -= 1;
        if (keyboardState.IsKeyDown(Keys.Right)) axisX += 1;
        if (keyboardState.IsKeyDown(Keys.Up)) axisY -= 1;
        if (keyboardState.IsKeyDown(Keys.Down)) axisY += 1;

        _bufferedLocalInput.AxisX = axisX;
        _bufferedLocalInput.AxisY = axisY;

        while (_stepTimer > StepTime)
        {
            _stepTimer -= StepTime;
            _deterministicGame.DeterministicStep(_bufferedLocalInput);
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

    private void DrawInGame()
    {
        _spriteBatch.Begin();
        _spriteBatch.Draw(_smileyTexture, _deterministicGame.PlayerPosition.ToVector2(), Color.White);
        _spriteBatch.Draw(_wallTexture, _deterministicGame.WallPosition.ToVector2(), Color.White);
        _spriteBatch.End();
    }

    protected override void Dispose(bool disposing)
    {
        _netPeer?.Dispose();

        base.Dispose(disposing);
    }
}