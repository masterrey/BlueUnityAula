using System;
using UnityEngine;

public class UnityLedSerialController : MonoBehaviour
{
    [Header("Serial")]
    [SerializeField] private string portName = "COM3";
    [SerializeField] private int baudRate = 9600;
    [SerializeField] private int writeTimeoutMs = 200;
    [SerializeField] private int openTimeoutMs = 500;
    [SerializeField] private int reconnectDelayMs = 250;
    [SerializeField] private bool connectOnStart = true;
    [SerializeField] private bool autoReconnectOnSendFail = true;
    [SerializeField] private bool resetArduinoOnConnect = true;

    private object serialPort;
    private Type serialPortType;

    private void Start()
    {
        if (connectOnStart)
        {
            Connect();
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnDisable()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void Connect()
    {
        if (IsOpen())
        {
            return;
        }

        try
        {
            if (!TryResolveSerialPortType())
            {
                Debug.LogError("System.IO.Ports.SerialPort não está disponível neste projeto/plataforma.");
                return;
            }

            CleanupPortObject();
            serialPort = Activator.CreateInstance(serialPortType, portName, baudRate);
            SetProperty("NewLine", "\n");
            SetProperty("WriteTimeout", writeTimeoutMs);
            SetProperty("ReadTimeout", openTimeoutMs);
            SetProperty("DtrEnable", resetArduinoOnConnect);
            SetProperty("RtsEnable", false);
            Invoke("Open");

            if (resetArduinoOnConnect)
            {
                System.Threading.Thread.Sleep(reconnectDelayMs);
                TryDiscardBuffers();
            }

            Debug.Log($"Serial conectada em {portName} @ {baudRate}");
        }
        catch (Exception ex)
        {
            CleanupPortObject();
            Debug.LogError($"Falha ao abrir serial {portName}: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        if (serialPort == null)
        {
            return;
        }

        try
        {
            if (IsOpen())
            {
                Invoke("Close");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Erro ao fechar serial: {ex.Message}");
        }
        finally
        {
            CleanupPortObject();
        }
    }

    public void Reconnect()
    {
        Disconnect();
        System.Threading.Thread.Sleep(reconnectDelayMs);
        Connect();
    }

    public void LigarLed()
    {
        SendCommand("LIGA");
    }

    public void DesligarLed()
    {
        SendCommand("DESLIGA");
    }

    public void ToggleLed(bool ligado)
    {
        SendCommand(ligado ? "LIGA" : "DESLIGA");
    }

    private void SendCommand(string command)
    {
        if (serialPort == null || !IsOpen())
        {
            Debug.LogWarning("Serial não conectada. Chame Connect() antes de enviar comandos.");
            return;
        }

        try
        {
            Invoke("WriteLine", command);
            Debug.Log($"Comando enviado: {command}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Falha ao enviar comando '{command}': {ex.Message}");

            if (!autoReconnectOnSendFail)
            {
                return;
            }

            Reconnect();

            if (serialPort == null || !IsOpen())
            {
                Debug.LogError("Reconexão falhou; comando não enviado.");
                return;
            }

            try
            {
                Invoke("WriteLine", command);
                Debug.Log($"Comando reenviado com sucesso: {command}");
            }
            catch (Exception retryEx)
            {
                Debug.LogError($"Falha após reconexão ao enviar '{command}': {retryEx.Message}");
            }
        }
    }

    private bool TryResolveSerialPortType()
    {
        if (serialPortType != null)
        {
            return true;
        }

        serialPortType = Type.GetType("System.IO.Ports.SerialPort, System");
        if (serialPortType == null)
        {
            serialPortType = Type.GetType("System.IO.Ports.SerialPort, System.IO.Ports");
        }

        return serialPortType != null;
    }

    private bool IsOpen()
    {
        if (serialPort == null)
        {
            return false;
        }

        var property = serialPort.GetType().GetProperty("IsOpen");
        if (property == null)
        {
            return false;
        }

        var value = property.GetValue(serialPort, null);
        return value is bool isOpen && isOpen;
    }

    private void SetProperty(string propertyName, object value)
    {
        var property = serialPort.GetType().GetProperty(propertyName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(serialPort, value, null);
        }
    }

    private object Invoke(string methodName, params object[] args)
    {
        var method = serialPort.GetType().GetMethod(methodName);
        if (method == null)
        {
            throw new MissingMethodException(serialPort.GetType().FullName, methodName);
        }

        return method.Invoke(serialPort, args);
    }

    private void TryDiscardBuffers()
    {
        try
        {
            Invoke("DiscardInBuffer");
        }
        catch
        {
        }

        try
        {
            Invoke("DiscardOutBuffer");
        }
        catch
        {
        }
    }

    private void CleanupPortObject()
    {
        if (serialPort == null)
        {
            return;
        }

        try
        {
            if (IsOpen())
            {
                Invoke("Close");
            }
        }
        catch
        {
        }

        try
        {
            Invoke("Dispose");
        }
        catch
        {
        }

        serialPort = null;
    }
}
