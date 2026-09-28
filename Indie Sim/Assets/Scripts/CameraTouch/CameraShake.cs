using Unity.Cinemachine;
using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance; // So you can call it from anywhere

    CinemachineCamera vcam;
    CinemachineBasicMultiChannelPerlin noise;

    void Awake()
    {
        // Make this accessible from anywhere
        if (Instance == null)
            Instance = this;
    }

    void Start()
    {
        vcam = GetComponent<CinemachineCamera>();
        // Get the noise component without generic type arguments
        noise = vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;

        // Debug check
        if (noise == null)
        {
            Debug.LogError("No CinemachineBasicMultiChannelPerlin found! Make sure Noise is enabled on your camera.");
        }
    }

    // Call this function when gun fires
    public void ShakeCamera(float intensity, float duration)
    {
        intensity *= SettingsService.Current.screenShake;
        if (noise != null && intensity > 0f)
        {
            StartCoroutine(DoShake(intensity, duration));
        }
    }

    IEnumerator DoShake(float intensity, float duration)
    {
        // Start shaking
        noise.AmplitudeGain = intensity;

        // Wait for the duration
        yield return new WaitForSeconds(duration);

        // Stop shaking
        noise.AmplitudeGain = 0f;
    }
}
