using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Org.BerkehanSari.ProceduralMotion.Tests
{
    public class WeaponAnimatorTests
    {
        private GameObject testObj;
        private ProWeaponAnimator animator;

        [SetUp]
        public void SetUp()
        {
            // Sandbox test object instantiation
            testObj = new GameObject("TestWeapon");
            animator = testObj.AddComponent<ProWeaponAnimator>();
            
            // Mock a configuration camera to avoid structural null reference exceptions during update ticks
            GameObject camObj = new GameObject("TestCamera");
            Camera camera = camObj.AddComponent<Camera>();
            animator.mainCamera = camera;
        }

        [TearDown]
        public void TearDown()
        {
            // Dynamic cleanup sequence to protect memory profiles
            Object.DestroyImmediate(testObj);
            if (animator.mainCamera != null)
            {
                Object.DestroyImmediate(animator.mainCamera.gameObject);
            }
        }

        [Test]
        public void Test_Initialization_SetsDefaultValues()
        {
            Assert.IsNotNull(animator);
            
            Assert.AreEqual(150f, animator.positionalStiffness, "Default positional stiffness should be initialized correctly.");
            Assert.AreEqual(14f, animator.positionalDamping, "Default positional damping should be initialized correctly.");
            Assert.AreEqual(150f, animator.rotationalStiffness, "Default rotational stiffness should be initialized correctly.");
            Assert.AreEqual(14f, animator.rotationalDamping, "Default rotational damping should be initialized correctly.");
        }

        [UnityTest]
        public IEnumerator Test_ApplyImpact_ModifiesTransformLocalPosition()
        {
            testObj.transform.localPosition = Vector3.zero;
            yield return null; // Wait for initial caching frame

            Vector3 proceduralForce = new Vector3(0.5f, -0.2f, 1.0f);
            animator.ApplyImpact(proceduralForce, Vector3.zero);

            // Yield control back to Unity engine to run LateUpdate calculations
            yield return new WaitForFixedUpdate();
            yield return null;

            // Assert that the procedural impact mathematics actually displaced the local position matrix
            Assert.AreNotEqual(Vector3.zero, testObj.transform.localPosition, 
                "Procedural impact must update and shift the local position matrix away from origin.");
        }

        [UnityTest]
        public IEnumerator Test_RecoilFire_AppliesFovKick()
        {
            float initialFov = animator.mainCamera.fieldOfView;
            
            animator.RecoilFire(Vector3.zero, Vector3.one);
            yield return null; // Tick update pipeline

            Assert.IsTrue(animator.mainCamera.fieldOfView > initialFov, 
                "Camera field of view state should structurally increase under recoil kinetic forces.");
        }
    }
}