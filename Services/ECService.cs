using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WardOneZeroFanControl.Services;

/// <summary>
/// Writes fan speed commands to the MSI Katana GF76's Embedded Controller (EC).
///
/// The EC is a tiny microcontroller (usually an ITE chip) on the motherboard.
/// It manages fans, LEDs, battery, and keyboard backlight.
/// 
/// Communication uses two I/O ports:
///   0x66 = EC Command port (we write commands here)
///   0x62 = EC Data port   (we read/write data here)
/// 
/// IMPORTANT: Run this application as Administrator, otherwise
/// port I/O will throw an UnauthorizedAccessException.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ECService
{
    // EC I/O port addresses (standard 8042-style EC protocol)
    private const int EC_SC = 0x66; // Status/Command port
    private const int EC_DATA = 0x62; // Data port

    // EC commands
    private const byte EC_CMD_READ = 0x80;
    private const byte EC_CMD_WRITE = 0x81;

    // MSI Katana GF76 EC register addresses for fan control
    // These were found by the open-source MSI Fan Control community.
    // Fan 1 = CPU fan, Fan 2 = GPU fan
    private const byte FAN1_SPEED_REG = 0xB0; // CPU fan speed register
    private const byte FAN2_SPEED_REG = 0xB1; // GPU fan speed register
    private const byte FAN_MODE_REG = 0xF4; // Fan control mode register
    private const byte FAN_MANUAL_MODE = 0x4D; // Value to enable manual mode

    private readonly ILogger<ECService> _logger;

    public ECService(ILogger<ECService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enables manual fan control mode on the EC.
    /// Must be called once before writing fan speeds.
    /// </summary>
    public void EnableManualControl()
    {
        try
        {
            ECWrite(FAN_MODE_REG, FAN_MANUAL_MODE);
            _logger.LogInformation("EC manual fan control enabled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable EC manual control. Are you running as Administrator?");
        }
    }

    /// <summary>
    /// Sets the fan speed for both CPU and GPU fans.
    /// </summary>
    /// <param name="percent">0–100 fan speed percentage</param>
    public void SetFanSpeed(int percent)
    {
        // EC expects a value 0-100 directly for these MSI registers
        byte value = (byte)Math.Clamp(percent, 20, 150);

        try
        {
            ECWrite(FAN1_SPEED_REG, value);
            ECWrite(FAN2_SPEED_REG, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write fan speed to EC.");
        }
    }

    /// <summary>
    /// Restores the EC to automatic (firmware-controlled) fan mode.
    /// Call this when the service shuts down — very important!
    /// Without this, fans stay at the last manually set speed forever.
    /// </summary>
    public void RestoreAutoControl()
    {
        try
        {
            ECWrite(FAN_MODE_REG, 0x00); // 0x00 = auto mode
            _logger.LogInformation("EC fan control restored to automatic.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore EC auto control.");
        }
    }

    // ── Low-level EC protocol implementation ──────────────────────────────

    /// <summary>
    /// Writes one byte to an EC register using the standard EC protocol.
    /// The EC is slow — we must wait for it to be ready (IBF/OBF flags).
    /// </summary>
    private static void ECWrite(byte register, byte value)
    {
        WaitInputBufferEmpty();
        Port.Out8(EC_SC, EC_CMD_WRITE);   // tell EC we want to write

        WaitInputBufferEmpty();
        Port.Out8(EC_DATA, register);      // send the register address

        WaitInputBufferEmpty();
        Port.Out8(EC_DATA, value);         // send the value
    }

    /// <summary>
    /// Reads one byte from an EC register.
    /// </summary>
    private static byte ECRead(byte register)
    {
        WaitInputBufferEmpty();
        Port.Out8(EC_SC, EC_CMD_READ);

        WaitInputBufferEmpty();
        Port.Out8(EC_DATA, register);

        WaitOutputBufferFull();
        return Port.In8(EC_DATA);
    }

    /// <summary>
    /// Waits until the EC input buffer is empty (bit 1 of status = 0).
    /// This means the EC is ready to receive a new byte.
    /// </summary>
    private static void WaitInputBufferEmpty()
    {
        int timeout = 1000;
        while ((Port.In8(EC_SC) & 0x02) != 0 && timeout-- > 0)
            Thread.SpinWait(10);
    }

    /// <summary>
    /// Waits until the EC output buffer has data (bit 0 of status = 1).
    /// </summary>
    private static void WaitOutputBufferFull()
    {
        int timeout = 1000;
        while ((Port.In8(EC_SC) & 0x01) == 0 && timeout-- > 0)
            Thread.SpinWait(10);
    }
}

/// <summary>
/// Thin wrapper around inpoutx64.dll — a free, open-source standalone
/// driver for x86 port I/O on Windows x64.
/// 
/// Download from: https://www.highrez.co.uk/downloads/inpout32/
/// Place inpoutx64.dll next to the EXE in C:\WardOneZero\FanControl\
/// 
/// Requires Administrator rights to install the kernel driver on first run.
/// After first run it stays installed as a Windows service automatically.
/// </summary>
internal static class Port
{
    [DllImport("inpoutx64.dll")]
    private static extern bool IsInpOutDriverOpen();

    [DllImport("inpoutx64.dll")]
    private static extern void Out32(short portAddress, short data);

    [DllImport("inpoutx64.dll")]
    private static extern short Inp32(short portAddress);

    static Port()
    {
        string dllPath = Path.Combine(AppContext.BaseDirectory, "inpoutx64.dll");

        if (!File.Exists(dllPath))
            throw new FileNotFoundException(
                $"inpoutx64.dll not found at: {dllPath}\n" +
                $"Download from https://www.highrez.co.uk/downloads/inpout32/ " +
                $"and place it in {AppContext.BaseDirectory}");

        // Install the kernel driver on first run (requires Admin)
        if (!IsInpOutDriverOpen())
            throw new InvalidOperationException(
                "inpoutx64 driver failed to open. Are you running as Administrator?");
    }

    public static void Out8(int port, byte value)
        => Out32((short)port, (short)value);

    public static byte In8(int port)
        => (byte)(Inp32((short)port) & 0xFF);

}