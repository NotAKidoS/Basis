using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public static class BasisXRManagement
{
    public static XRManagerSettings XRInstance;
    /// <summary>
    /// pulled in from InputDevices.GetDevices
    /// </summary>
    public static List<InputDevice> inputDevices = new List<InputDevice>();
    /// <summary>
    /// generated at runtime
    /// </summary>
    public static List<BasisXRInput> TrackedXRInputDevices = new List<BasisXRInput>();
    /// <summary>
    /// keeps track of generated IDs and match InputDevice
    /// </summary>
    public static Dictionary<string, InputDevice> TypicalDevices = new Dictionary<string, InputDevice>();
    public static bool TryStartXR()
    {
        //finds the first working vr loader
        XRGeneralSettings.Instance.Manager.InitializeLoaderSync();
        //start its subsystems
        XRInstance = XRGeneralSettings.Instance.Manager;
        StartXRSDK();
        if (XRInstance.activeLoader == null)
        {
            return false;
        }
        return true;
    }
    public static void StopXR()
    {
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
        {
            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
            StopXRSDK();
        }
        UnityEngine.XR.InputDevices.deviceConnected -= OnDeviceConnected;
        UnityEngine.XR.InputDevices.deviceDisconnected -= OnDeviceDisconnected;
    }
    private static void StartXRSDK()
    {
        if (XRInstance != null && XRInstance.activeLoader != null)
        {
            XRInstance.StartSubsystems();
        }
        UnityEngine.XR.InputDevices.deviceConnected += OnDeviceConnected;
        UnityEngine.XR.InputDevices.deviceDisconnected += OnDeviceDisconnected;
    }
    private static void StopXRSDK()
    {
        if (XRInstance != null && XRInstance.activeLoader != null)
        {
            XRInstance.StopSubsystems();
        }
    }

    private static void OnDeviceConnected(UnityEngine.XR.InputDevice device)
    {
        UpdateDeviceList();
    }

    private static void OnDeviceDisconnected(UnityEngine.XR.InputDevice device)
    {
        UpdateDeviceList();
    }
    private static void UpdateDeviceList()
    {
        InputDevices.GetDevices(inputDevices);
        foreach (UnityEngine.XR.InputDevice device in inputDevices)
        {
            if (device != null)
            {
                string ID = GenerateID(device);
                if (TypicalDevices.ContainsKey(ID) == false)
                {
                    CreatePhysicalTrackedDevice(device, ID);
                    TypicalDevices.Add(ID, device);
                }
            }
        }
        foreach (var deviceData in TypicalDevices)
        {
            if (deviceData.Value == null)
            {
                string ID = deviceData.Key;
                DestroyPhysicalTrackedDevice(ID);
            }
        }
    }
    public static string GenerateID(UnityEngine.XR.InputDevice device)
    {
        string ID = device.name + "|" + device.serialNumber + "|" + device.manufacturer + "|" + (int)device.characteristics;
        return ID;
    }
    public static void CreatePhysicalTrackedDevice(UnityEngine.XR.InputDevice device, string ID)
    {
        GameObject gameObject = new GameObject(ID);
        gameObject.transform.parent = BasisLocalPlayer.Instance.LocalBoneDriver.transform;
        BasisXRInput BasisXRInput = gameObject.AddComponent<BasisXRInput>();
        BasisXRInput.Initialize(device, ID);
        TrackedXRInputDevices.Add(BasisXRInput);
    }
    /// <summary>
    /// this wont well with fullbody, revist later
    /// </summary>
    /// <param name="ID"></param>
    public static void DestroyPhysicalTrackedDevice(string ID)
    {
        DestroyInputDevice(ID);
        DestroyXRInput(ID);
    }
    public static void DestroyInputDevice(string ID)
    {
        foreach (var device in TypicalDevices)
        {
            if (device.Key == ID)
            {
                TypicalDevices.Remove(ID);
                break;
            }
        }
    }
    public static void DestroyXRInput(string ID)
    {
        foreach (var device in TrackedXRInputDevices)
        {
            if (device.ID == ID)
            {
                TrackedXRInputDevices.Remove(device);
                break;
            }
        }
    }
}