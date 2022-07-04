using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haiku.Rando
{
    public static class SceneUtils
    {
        private static int _lastSceneId;
        private static GameObject[] _sceneRoots;
        
        public static T[] FindObjectsOfType<T>()
            where T: class
        {
            int sceneId = SceneManager.GetActiveScene().buildIndex;
            if (sceneId != _lastSceneId)
            {
                _lastSceneId = sceneId;
                _sceneRoots = SceneManager.GetActiveScene().GetRootGameObjects();
            }

            return _sceneRoots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        }

        public static T FindObjectOfType<T>()
            where T: class
        {
            int sceneId = SceneManager.GetActiveScene().buildIndex;
            if (sceneId != _lastSceneId)
            {
                _lastSceneId = sceneId;
                _sceneRoots = SceneManager.GetActiveScene().GetRootGameObjects();
            }

            return _sceneRoots.Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);
        }
    }
}
