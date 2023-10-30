using System.Collections.Generic;
using UnityEngine;


namespace PupilLabs
{
    public class ServiceLocator : MonoBehaviour
    {
        public static ServiceLocator Instance;

        [SerializeField]
        private bool dontDestroyOnLoad = true;
        [SerializeField]
        private GazeDataProvider gazeDataProvider; //fast access to core functionality, rest of the objects should be located via GetComponentInChildren, include inactive if using in awake

        public GazeDataProvider GazeDataProvider { get { return gazeDataProvider; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            foreach (Transform t in transform)
            {
                t.gameObject.SetActive(true);
            }
            if (gazeDataProvider == null)
            {
                gazeDataProvider = GetComponentInChildren<GazeDataProvider>();
            }
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
    }
}