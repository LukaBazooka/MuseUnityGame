using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class BlinkJawUI : MonoBehaviour
{
    [Header("UI References")]
    public Button connectButton;
    public Button disconnectButton;
    public TextMeshProUGUI statusText;
    public Image topSquare;
    public Image bottomSquare;

    [Header("Visual Settings")]
    [Tooltip("How long the square colors stay visible (in seconds)")]
    public float colorDisplayDuration = 0.5f;

    private void Start()
    {
        connectButton.onClick.AddListener(OnConnectClicked);
        disconnectButton.onClick.AddListener(OnDisconnectClicked);
        DisconnectUI();
    }

    private async void OnConnectClicked()
    {
        statusText.text = "Connecting...";
        await MuseService.Instance.Connect();
        if (MuseService.Instance.IsConnected)
        {
            SetupEventListeners();
            ConnectUI();
        }
        else statusText.text = "Connection Failed";
    }

    private async void OnDisconnectClicked()
    {
        await MuseService.Instance.Disconnect();
        TearDownEventListeners();
        DisconnectUI();
    }

    private void SetupEventListeners()
    {
        MuseService.Instance.OnBlink += HandleBlink;
        MuseService.Instance.OnJawClench += HandleJawClench;
    }

    private void TearDownEventListeners()
    {
        if (MuseService.Instance != null)
        {
            MuseService.Instance.OnBlink -= HandleBlink;
            MuseService.Instance.OnJawClench -= HandleJawClench;
        }
    }

    private void HandleBlink() 
    {
        topSquare.color = Color.red;
        StartCoroutine(ResetSquareColor(topSquare));
    }
    
    private void HandleJawClench() 
    {
        bottomSquare.color = Color.blue;
        StartCoroutine(ResetSquareColor(bottomSquare));
    }

    private IEnumerator ResetSquareColor(Image square)
    {
        yield return new WaitForSeconds(colorDisplayDuration);
        square.color = Color.white;
    }

    private void ConnectUI()
    {
        statusText.text = "Connected";
        connectButton.gameObject.SetActive(false);
        disconnectButton.gameObject.SetActive(true);
    }

    private void DisconnectUI()
    {
        statusText.text = "Disconnected";
        connectButton.gameObject.SetActive(true);
        disconnectButton.gameObject.SetActive(false);
        // reset colors
        topSquare.color = Color.white;
        bottomSquare.color = Color.white;
    }
}
