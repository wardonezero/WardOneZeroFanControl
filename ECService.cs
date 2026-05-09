using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WardOneZeroFanControl;

[SupportedOSPlatform("windows")]
public sealed class ECService(ILogger<ECService> logger)
{
    private const int EC_SC = 0x66; // Status/Command
    private const int EC_DATA = 0x62; // Data

    // EC commands
    private const byte EC_CMD_READ = 0x80;
    private const byte EC_CMD_WRITE = 0x81;

    // MSI Katana GF76 EC Temperature Registers (READ ONLY)
    private const byte CPU_TEMP_REG = 0x68;
    private const byte GPU_TEMP_REG = 0x80;

    // MSI Katana GF76 EC Fan Speed Registers (WRITE)
    private const byte FAN1_DUTY_REG = 0xB0; // CPU fan duty %
    private const byte FAN2_DUTY_REG = 0xB1; // GPU fan duty %

    // MSI Katana GF76 EC Fan Mode Register
    private const byte FAN_MODE_REG = 0xF4; // Fan Control Mode Register
    private const byte FAN_MANUAL_MODE = 0x4D; // EC Manual Fan Control
    private const byte FAN_AUTO_MODE = 0x0D; // EC Auto Fan Control

    private readonly ILogger<ECService> _logger = logger;

    public float? ReadCPUTemperature()
    {
        try
        {
            byte raw = ECRead(CPU_TEMP_REG);
            return raw == 0 || raw == 0xFF ? null : raw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Faild to Read CPU temperature from EC");
            return null;
        }
    }

    public float? ReadGPUTemperature()
    {
        try
        {
            byte raw = ECRead(GPU_TEMP_REG);
            return raw == 0 || raw == 0xFF ? null : raw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read GPU temperature from EC");
            return null;
        }
    }

    public void EnableManualFanControl()
    {
        try
        {
            ECWrite(FAN_MODE_REG, FAN_MANUAL_MODE);
            _logger.LogInformation("EC: Manual fan control enabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable EC manual control");
        }
    }

    public void SetFanSpeed(byte percent)
    {
        byte value = Math.Clamp(percent, (byte)20, (byte)100);
        try
        {
            ECWrite(FAN1_DUTY_REG, value);
            ECWrite(FAN2_DUTY_REG, value);
            _logger.LogDebug("EC: Fan speed written → {Percent}%", value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write fan speed to EC.");
        }
    }

    public void RestoreAutoFanControl()
    {
        try
        {
            ECWrite(FAN_MODE_REG, FAN_AUTO_MODE);
            _logger.LogInformation("EC: Auto fan control restored");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore EC auto fan control");
        }
    }

    private static void ECWrite(byte register, byte value)
    {
        WaitInputBufferEmpty();
        Port.Out8(EC_SC, EC_CMD_WRITE);

        WaitInputBufferEmpty();
        Port.Out8(EC_DATA, register);

        WaitInputBufferEmpty();
        Port.Out8(EC_DATA, value);
    }

    private static byte ECRead(byte register)
    {
        WaitInputBufferEmpty();
        Port.Out8(EC_SC, EC_CMD_READ);

        WaitInputBufferEmpty();
        Port.Out8(EC_DATA, register);

        WaitOutputBufferFull();
        return Port.In8(EC_DATA);
    }

    private static void WaitInputBufferEmpty()
    {
        int timeout = 1000;
        while ((Port.In8(EC_SC) & 0x02) != 0 && timeout-- > 0)
            Thread.SpinWait(10);
    }

    private static void WaitOutputBufferFull()
    {
        int timeout = 1000;
        while ((Port.In8(EC_SC) & 0x01) == 0 && timeout-- > 0)
            Thread.SpinWait(10);
    }
}

internal static class Port
{
    [DllImport("inpoutx64.dll")] private static extern bool IsInpOutDriverOpen();
    [DllImport("inpoutx64.dll")] private static extern void Out32(short portAddress, short data);
    [DllImport("inpoutx64.dll")] private static extern short Inp32(short portAddress);

    static Port()
    {
        string dllPath = Path.Combine(AppContext.BaseDirectory, "inpoutx64.dll");

        if (!File.Exists(dllPath))
            throw new FileNotFoundException(
                $"inpoutx64.dll not found at: {dllPath}\n" +
                "Download from https://www.highrez.co.uk/" +
                $"and place it in {AppContext.BaseDirectory}");

        if (!IsInpOutDriverOpen())
            throw new InvalidOperationException(
                "inpoutx64 driver failed to open. Run as Administrator");
    }

    public static void Out8(int port, byte value) => Out32((short)port, value);
    public static byte In8(int port) => (byte)(Inp32((short)port) & 0xFF);
}