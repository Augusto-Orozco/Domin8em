using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private const string WeaponResourcePath = "Prefabs/GunsPrefabs/M4";
    private const string BulletResourcePath = "Prefabs/GunsPrefabs/M4BulletV3";
    private const string WeaponSpawnPointResourcePath = "Prefabs/GunsPrefabs/GunSpawnPoint";

    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float ExternalSpeedMultiplier { get; set; } = 1f;
    public float gravity = -20f;
    public bool readInput = true;

    [Header("Cámara y mouse")]
    [Tooltip("Asigna la cámara o el pivote de cámara que pertenece al personaje.")]
    public Transform cameraTransform;
    public float mouseSensitivity = 15f;
    public float minPitch = -35f;
    public float maxPitch = 60f;
    [Tooltip("Altura de los ojos medida desde la base del CharacterController.")]
    [Min(0f)] public float cameraHeight = 1.6f;

    [Header("Arma")]
    [Tooltip("Punto de aparición del arma; si no se asigna, se carga GunSpawnPoint desde Resources.")]
    public Transform weaponSpawnPoint;
    public Vector3 weaponLocalPosition = new Vector3(0.25f, -0.2f, 0.5f);
    public Vector3 weaponLocalRotation;
    [Min(0.01f)] public float shotInterval = 0.12f;
    [Min(0.1f)] public float bulletSpeed = 80f;
    [Min(0.1f)] public float bulletLifetime = 5f;
    public bool hidePlayerRenderersInFirstPerson = true;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private float cameraPitch;
    private bool hasBubbleGun;
    private bool shootingActive;
    private float nextShotTime;
    private Transform muzzle;
    private AimPlayer aimPlayer;
    private GameObject bulletPrefab;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        SetupFirstPersonCamera();
        HidePlayerRenderers();
        EquipWeapon();
    }

    private void Update()
    {
        if (readInput)
        {
            HandleMouseLook();
            HandleMovement();
        }

        HandleGravity();

        if (readInput && Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            TryShoot();
        }

        if (shootingActive)
        {
            TryShoot();
        }
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity * Time.deltaTime;
        transform.Rotate(Vector3.up * mouseDelta.x);

        if (cameraTransform != null)
        {
            cameraPitch -= mouseDelta.y;
            cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
            cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        float forwardInput = 0f;
        float strafeInput = 0f;

        if (Keyboard.current.wKey.isPressed)
        {
            forwardInput += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            forwardInput -= 1f;
        }

        if (Keyboard.current.aKey.isPressed)
        {
            strafeInput -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            strafeInput += 1f;
        }

        Vector3 moveDirection = (transform.forward * forwardInput + transform.right * strafeInput).normalized;
        controller.Move(moveDirection * moveSpeed * ExternalSpeedMultiplier * Time.deltaTime);
    }

    private void HandleGravity()
    {
        if (controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private void EquipWeapon()
    {
        if (cameraTransform == null)
        {
            Debug.LogError("PlayerMovement no encontró una cámara para equipar el arma.", this);
            return;
        }

        SetupWeaponSpawnPoint();

        GameObject weaponPrefab = Resources.Load<GameObject>(WeaponResourcePath);
        bulletPrefab = Resources.Load<GameObject>(BulletResourcePath);
        if (weaponPrefab == null || bulletPrefab == null)
        {
            Debug.LogError(
                $"No se encontraron los prefabs requeridos en Resources: '{WeaponResourcePath}' y '{BulletResourcePath}'.",
                this
            );
            return;
        }

        GameObject weapon = Instantiate(weaponPrefab, weaponSpawnPoint, false);
        weapon.transform.localPosition = Vector3.zero;

        aimPlayer = weapon.GetComponentInChildren<AimPlayer>(true);
        foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Muzzle")
            {
                muzzle = child;
                break;
            }
        }

        if (aimPlayer == null || muzzle == null)
        {
            Debug.LogError("El prefab M4 debe incluir AimPlayer y un objeto hijo llamado Muzzle.", weapon);
            Destroy(weapon);
            return;
        }

        aimPlayer.SetWeaponActive(true);
        hasBubbleGun = true;
    }

    private void SetupFirstPersonCamera()
    {
        if (cameraTransform == null || cameraTransform == transform)
        {
            return;
        }

        cameraTransform.SetParent(transform, false);
        float heightFromControllerBottom = controller.center.y - controller.height * 0.5f + cameraHeight;
        cameraTransform.localPosition = new Vector3(0f, heightFromControllerBottom, 0f);
        cameraTransform.localRotation = Quaternion.identity;
        cameraPitch = 0f;
    }

    private void HidePlayerRenderers()
    {
        if (!hidePlayerRenderersInFirstPerson)
        {
            return;
        }

        foreach (Renderer playerRenderer in GetComponentsInChildren<Renderer>())
        {
            playerRenderer.enabled = false;
        }
    }

    private void SetupWeaponSpawnPoint()
    {
        if (weaponSpawnPoint == null)
        {
            weaponSpawnPoint = transform.Find("GunSpawnPoint");
        }

        if (weaponSpawnPoint == null)
        {
            GameObject spawnPointPrefab = Resources.Load<GameObject>(WeaponSpawnPointResourcePath);
            if (spawnPointPrefab != null)
            {
                weaponSpawnPoint = Instantiate(spawnPointPrefab).transform;
            }
            else
            {
                Debug.LogWarning(
                    $"No se encontró '{WeaponSpawnPointResourcePath}'; se usará la posición de arma configurada en el Inspector.",
                    this
                );

                GameObject fallbackSpawnPoint = new GameObject("GunSpawnPoint");
                weaponSpawnPoint = fallbackSpawnPoint.transform;
                weaponSpawnPoint.SetParent(cameraTransform, false);
                weaponSpawnPoint.localPosition = weaponLocalPosition;
                weaponSpawnPoint.localRotation = Quaternion.Euler(weaponLocalRotation);
            }
        }

        if (weaponSpawnPoint != cameraTransform && !weaponSpawnPoint.IsChildOf(cameraTransform))
        {
            weaponSpawnPoint.SetParent(cameraTransform, true);
        }
    }

    public void EquipBubbleGun()
    {
        if (!hasBubbleGun)
        {
            EquipWeapon();
        }
    }

    public bool HasBubbleGun()
    {
        return hasBubbleGun;
    }

    public void StartShooting()
    {
        TryShoot();
    }

    public void SetShootingActive(bool active)
    {
        shootingActive = active;
        if (active)
        {
            TryShoot();
        }
    }

    private void TryShoot()
    {
        if (!hasBubbleGun || aimPlayer == null || muzzle == null || bulletPrefab == null || Time.time < nextShotTime)
        {
            return;
        }

        Vector3 direction = aimPlayer.GetAimDirection(muzzle);
        GameObject bullet = Instantiate(
            bulletPrefab,
            muzzle.position,
            Quaternion.LookRotation(direction)
        );

        Rigidbody bulletBody = bullet.GetComponent<Rigidbody>();
        if (bulletBody == null)
        {
            bulletBody = bullet.AddComponent<Rigidbody>();
        }

        Collider bulletCollider = bullet.GetComponent<Collider>();
        if (bulletCollider == null)
        {
            SphereCollider sphereCollider = bullet.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.15f;
            bulletCollider = sphereCollider;
        }

        bulletBody.useGravity = false;
        bulletBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        bulletBody.linearVelocity = direction.normalized * bulletSpeed;

        foreach (Collider playerCollider in GetComponentsInChildren<Collider>())
        {
            if (playerCollider != null)
            {
                Physics.IgnoreCollision(bulletCollider, playerCollider);
            }
        }

        Destroy(bullet, bulletLifetime);
        nextShotTime = Time.time + Mathf.Max(0.01f, shotInterval);
    }
}
