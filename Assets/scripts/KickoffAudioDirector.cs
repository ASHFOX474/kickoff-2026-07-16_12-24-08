using UnityEngine;

/// <summary>
/// Zero-setup sound. Auto-spawns itself in every scene that has a GameManager,
/// synthesizes its clips at runtime via ProceduralAudio, and reacts to
/// GameManager / GuardAI state - all through their existing public fields,
/// so GameManager.cs and GuardAI.cs don't need to be touched at all.
/// Footsteps while moving, a stinger when a guard clocks you, a whistle if
/// you're caught, a chime if you win.
/// </summary>
public class KickoffAudioDirector : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<KickoffAudioDirector>() != null) return;
        if (FindFirstObjectByType<GameManager>() == null) return; // menu/intro scenes don't need this

        GameObject go = new GameObject("KickoffAudioDirector");
        go.AddComponent<KickoffAudioDirector>();
    }

    private AudioSource sfxSource;
    private AudioSource footstepSource;
    private AudioClip alertClip, caughtClip, winClip, footstepClip;

    private GuardAI[] guards = new GuardAI[0];
    private GuardAI.State[] lastStates = new GuardAI.State[0];
    private float guardRefreshTimer;
    private bool caughtPlayed;
    private bool winPlayed;
    private PlayerController player;
    private float footstepTimer;

    private void Awake()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.volume = 0.6f;

        alertClip = ProceduralAudio.AlertStinger();
        caughtClip = ProceduralAudio.CaughtWhistle();
        winClip = ProceduralAudio.WinChime();
        footstepClip = ProceduralAudio.Footstep();
    }

    private void Update()
    {
        RefreshGuards();
        CheckGuardStates();
        CheckGameOutcome();
        HandleFootsteps();
    }

    private void RefreshGuards()
    {
        guardRefreshTimer -= Time.unscaledDeltaTime;
        if (guardRefreshTimer > 0f && guards.Length > 0) return;
        guardRefreshTimer = 0.5f;

        GuardAI[] found = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        if (found.Length != guards.Length)
        {
            guards = found;
            lastStates = new GuardAI.State[guards.Length];
            for (int i = 0; i < guards.Length; i++)
            {
                lastStates[i] = guards[i] != null ? guards[i].currentState : GuardAI.State.Patrol;
            }
        }
    }

    private void CheckGuardStates()
    {
        for (int i = 0; i < guards.Length; i++)
        {
            if (guards[i] == null) continue;
            GuardAI.State state = guards[i].currentState;
            if (state != lastStates[i])
            {
                if (state == GuardAI.State.Alert || state == GuardAI.State.Chase)
                {
                    sfxSource.PlayOneShot(alertClip, 0.8f);
                }
                lastStates[i] = state;
            }
        }
    }

    private void CheckGameOutcome()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsGameOver) return;

        if (GameManager.Instance.PlayerWon && !winPlayed)
        {
            winPlayed = true;
            sfxSource.PlayOneShot(winClip, 0.9f);
        }
        else if (!GameManager.Instance.PlayerWon && !caughtPlayed)
        {
            caughtPlayed = true;
            sfxSource.PlayOneShot(caughtClip, 0.9f);
        }
    }

    private void HandleFootsteps()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
        }

        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
        {
            footstepTimer = 0f;
            return;
        }

        float speed = player.CurrentMoveInput.magnitude;
        if (speed > 0.1f)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                footstepTimer = Mathf.Lerp(0.42f, 0.24f, Mathf.Clamp01(speed));
                footstepSource.PlayOneShot(footstepClip, 0.5f);
            }
        }
        else
        {
            footstepTimer = 0f;
        }
    }
}
