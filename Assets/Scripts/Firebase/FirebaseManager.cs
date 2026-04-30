using UnityEngine;
using Firebase;
using Firebase.Extensions;
using Unity.VisualScripting;
using System;

public class FirebaseManager : GlobalSingleton<FirebaseManager>
{
    public bool InReady { get; private set; }

    private FirebaseApp firebaseApp;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this) return;

        InitFirebase();
    }


    /// <summary>
    /// Firebase 초기화
    /// </summary>
    private void InitFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                firebaseApp = FirebaseApp.DefaultInstance;
                InReady = true;

                Debug.Log("[Firebase] Initialized successfully.");
            }
            else
            {
                InReady = false;
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        });
    }
}