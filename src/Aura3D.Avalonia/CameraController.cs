using Avalonia;
using Avalonia.Input;
using Aura3D.Core.Nodes;
using System.Numerics;

namespace Aura3D.Avalonia;

/// <summary>
/// 相机控制器，提供鼠标和键盘对场景相机的操控能力，
/// 支持 WASD 移动、鼠标拖拽旋转/平移、滚轮缩放。
/// </summary>
/// <remarks>
/// 使用方式：
/// <code>
/// _cameraController = new CameraController(aura3dView);
/// // 可选：自定义参数
/// _cameraController.MoveSpeed = 20f;
/// _cameraController.Camera = someOtherCamera;
/// </code>
/// </remarks>
public class CameraController : IDisposable
{
    private readonly Aura3DViewBase _view;
    private readonly HashSet<Key> _pressedKeys = new();

    private bool _isLooking;
    private bool _isPanning;
    private Point _lastMousePoint;
    private double _deltaTime;

    private Camera? _camera;
    private bool _disposed;

    // ==================== 构造与生命周期 ====================

    /// <summary>
    /// 初始化 <see cref="CameraController"/> 的新实例，并自动附加到指定视图。
    /// </summary>
    /// <param name="view">要操控的 Aura3D 视图。</param>
    public CameraController(Aura3DViewBase view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _view.Focusable = true;

        _view.KeyDown += OnKeyDown;
        _view.KeyUp += OnKeyUp;
        _view.PointerPressed += OnPointerPressed;
        _view.PointerReleased += OnPointerReleased;
        _view.PointerMoved += OnPointerMoved;
        _view.PointerWheelChanged += OnPointerWheelChanged;
        _view.AddHandler(Aura3DView.OnSceneUpdatedEvent, (EventHandler<UpdateRoutedEventArgs>)OnSceneUpdated);
    }

    /// <summary>
    /// 释放控制器占用的资源，取消所有事件订阅。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _view.KeyDown -= OnKeyDown;
        _view.KeyUp -= OnKeyUp;
        _view.PointerPressed -= OnPointerPressed;
        _view.PointerReleased -= OnPointerReleased;
        _view.PointerMoved -= OnPointerMoved;
        _view.PointerWheelChanged -= OnPointerWheelChanged;
        _view.RemoveHandler(Aura3DView.OnSceneUpdatedEvent, (EventHandler<UpdateRoutedEventArgs>)OnSceneUpdated);
    }

    // ==================== 属性 ====================

    /// <summary>
    /// 获取或设置要操控的相机。默认为 <c>null</c>，此时使用视图的 <see cref="Aura3DViewBase.MainCamera"/>。
    /// </summary>
    public Camera? Camera
    {
        get => _camera;
        set => _camera = value;
    }

    /// <summary>
    /// 获取当前实际使用的相机实例。
    /// </summary>
    private Camera ActiveCamera => _camera ?? _view.MainCamera;

    // ---- 速度与灵敏度 ----

    /// <summary>
    /// 键盘移动速度（单位/秒），默认 10。
    /// </summary>
    public float MoveSpeed { get; set; } = 10f;

    /// <summary>
    /// 鼠标旋转灵敏度，默认 20。值越大旋转越快。
    /// </summary>
    public float MouseSensitivity { get; set; } = 20f;

    /// <summary>
    /// 鼠标平移速度，默认 10。
    /// </summary>
    public float PanSpeed { get; set; } = 10f;

    /// <summary>
    /// 滚轮缩放速度，默认 5。
    /// </summary>
    public float ZoomSpeed { get; set; } = 5f;

    // ---- 按键绑定 ----

    /// <summary>
    /// 向前移动按键，默认 <see cref="Key.W"/>。
    /// </summary>
    public Key MoveForwardKey { get; set; } = Key.W;

    /// <summary>
    /// 向后移动按键，默认 <see cref="Key.S"/>。
    /// </summary>
    public Key MoveBackwardKey { get; set; } = Key.S;

    /// <summary>
    /// 向左移动按键，默认 <see cref="Key.A"/>。
    /// </summary>
    public Key MoveLeftKey { get; set; } = Key.A;

    /// <summary>
    /// 向右移动按键，默认 <see cref="Key.D"/>。
    /// </summary>
    public Key MoveRightKey { get; set; } = Key.D;

    /// <summary>
    /// 向上移动按键，默认 <see cref="Key.E"/>。
    /// </summary>
    public Key MoveUpKey { get; set; } = Key.E;

    /// <summary>
    /// 向下移动按键，默认 <see cref="Key.Q"/>。
    /// </summary>
    public Key MoveDownKey { get; set; } = Key.Q;

    // ---- 鼠标绑定 ----

    /// <summary>
    /// 旋转视角的鼠标按键，默认 <see cref="MouseButton.Right"/>（右键拖拽旋转）。
    /// </summary>
    public MouseButton LookButton { get; set; } = MouseButton.Right;

    /// <summary>
    /// 平移视角的鼠标按键，默认 <see cref="MouseButton.Middle"/>（中键拖拽平移）。
    /// </summary>
    public MouseButton PanButton { get; set; } = MouseButton.Middle;

    // ---- 功能开关 ----

    /// <summary>
    /// 总开关。设为 <c>false</c> 时禁用所有操控，默认 <c>true</c>。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否允许鼠标旋转视角，默认 <c>true</c>。
    /// </summary>
    public bool EnableLook { get; set; } = true;

    /// <summary>
    /// 是否允许键盘移动，默认 <c>true</c>。
    /// </summary>
    public bool EnableMovement { get; set; } = true;

    /// <summary>
    /// 是否允许滚轮缩放，默认 <c>true</c>。
    /// </summary>
    public bool EnableZoom { get; set; } = true;

    /// <summary>
    /// 是否允许鼠标平移，默认 <c>true</c>。
    /// </summary>
    public bool EnablePan { get; set; } = true;

    // ==================== 帧更新 ====================

    /// <summary>
    /// 每帧调用，处理持续按住按键的移动。
    /// </summary>
    private void OnSceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        _deltaTime = e.DeltaTime;

        if (!Enabled || _view.Scene == null)
            return;

        if (EnableMovement && _pressedKeys.Count > 0)
        {
            ApplyKeyboardMovement((float)e.DeltaTime);
        }
    }

    // ==================== 键盘事件 ====================

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!Enabled || _view.Scene == null)
            return;

        _pressedKeys.Add(e.Key);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Remove(e.Key);
    }

    // ==================== 鼠标事件 ====================

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!Enabled || _view.Scene == null)
            return;

        var point = e.GetCurrentPoint(_view);
        var pos = point.Position;

        if (IsButtonPressed(point.Properties, LookButton))
        {
            _isLooking = true;
            _lastMousePoint = pos;
        }

        if (IsButtonPressed(point.Properties, PanButton))
        {
            _isPanning = true;
            _lastMousePoint = pos;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == LookButton)
        {
            _isLooking = false;
            _lastMousePoint = new Point(-1, -1);
        }

        if (e.InitialPressMouseButton == PanButton)
        {
            _isPanning = false;
            _lastMousePoint = new Point(-1, -1);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!Enabled || _view.Scene == null)
            return;

        var pos = e.GetPosition(_view);

        if (_lastMousePoint.X < 0)
        {
            // 首次移动，仅记录位置不做旋转
            _lastMousePoint = pos;
            return;
        }

        var delta = pos - _lastMousePoint;
        _lastMousePoint = pos;

        var cam = ActiveCamera;
        var dt = (float)_deltaTime;

        if (_isLooking && EnableLook)
        {
            cam.RotationDegrees = new Vector3(
                cam.RotationDegrees.X + (float)delta.Y * dt * MouseSensitivity,
                cam.RotationDegrees.Y + (float)delta.X * dt * MouseSensitivity,
                0);
        }

        if (_isPanning && EnablePan)
        {
            cam.Position -= cam.Right * (float)delta.X * dt * PanSpeed;
            cam.Position += cam.Up * (float)delta.Y * dt * PanSpeed;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!Enabled || _view.Scene == null || !EnableZoom)
            return;

        var cam = ActiveCamera;
        cam.Position += cam.Forward * (float)e.Delta.Y * ZoomSpeed * 0.01f;
    }

    // ==================== 内部方法 ====================

    /// <summary>
    /// 根据当前按下的按键执行键盘移动。
    /// </summary>
    private void ApplyKeyboardMovement(float deltaTime)
    {
        var dir = Vector3.Zero;
        var cam = ActiveCamera;

        if (_pressedKeys.Contains(MoveForwardKey))
            dir += cam.Forward;
        if (_pressedKeys.Contains(MoveBackwardKey))
            dir -= cam.Forward;
        if (_pressedKeys.Contains(MoveRightKey))
            dir += cam.Right;
        if (_pressedKeys.Contains(MoveLeftKey))
            dir -= cam.Right;
        if (_pressedKeys.Contains(MoveUpKey))
            dir += cam.Up;
        if (_pressedKeys.Contains(MoveDownKey))
            dir -= cam.Up;

        if (dir != Vector3.Zero)
        {
            cam.Position += Vector3.Normalize(dir) * MoveSpeed * deltaTime;
        }
    }

    private static bool IsButtonPressed(PointerPointProperties props, MouseButton button) => button switch
    {
        MouseButton.Left => props.IsLeftButtonPressed,
        MouseButton.Right => props.IsRightButtonPressed,
        MouseButton.Middle => props.IsMiddleButtonPressed,
        MouseButton.XButton1 => props.IsXButton1Pressed,
        MouseButton.XButton2 => props.IsXButton2Pressed,
        _ => false,
    };
}
