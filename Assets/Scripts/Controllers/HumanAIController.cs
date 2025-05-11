using UnityEngine;
using Unity.Mathematics;
using ApplicationManagers;
using Settings;
using Characters;
using System.Collections.Generic;
using Utility;
using Photon.Pun;
using Map;
using Random = UnityEngine.Random;
using System;

namespace Controllers
{
    class HumanAIController : BaseAIController
    {
        protected Human _human;
        public Human Human
        {
            get { return _human; }
        }
        // protected HumanInputSettings _humanInput;
        public static readonly LayerMask BarrierMask = PhysicsLayer.GetMask(PhysicsLayer.Human, PhysicsLayer.TitanPushbox, PhysicsLayer.ProjectileDetection,
            PhysicsLayer.MapObjectProjectiles, PhysicsLayer.MapObjectEntities, PhysicsLayer.MapObjectAll);
        public static readonly LayerMask MapMask = PhysicsLayer.GetMask(PhysicsLayer.TitanPushbox, PhysicsLayer.MapObjectProjectiles, PhysicsLayer.MapObjectEntities, PhysicsLayer.MapObjectAll);

        public Vector3? MoveDirection = null;

        public Vector3 AimDirection;

        public Vector3? DashDirection = null;

        public bool DoJump = false;

        public bool DoAttack = false;
        public bool DoSpecial = false;

        public bool DoHorseMount = false;

        public bool DoDodge = false;

        public bool DoReload = false;

        public int ReelAxis = 0;

        public bool DoHookLeft = false;
        public bool DoHookLeftHooked = false;
        public bool DoHookRight = false;
        public bool DoHookRightHooked = false;

        public float _hookLeftTimer = 0f;
        public float _hookRightTimer = 0f;

        public HumanAIAutomaton Automaton;

        protected ITargetable _target;

        public ITargetable Target
        {
            get
            {
                return _target;
            }
            set
            {
                _target = value;
                if (_target != null)
                {
                    TargetVelocity = Vector3.zero;
                    _targetLastPosition = _target.GetPosition();
                }
            }
        }

        public Vector3 TargetPosition;
        public Vector3 TargetDirection;

        protected Vector3? _targetLastPosition;
        public Vector3 TargetVelocity;

        public float LockingDistance = 100f;

        public float DetectRange = 10000f;

        public float HookSpeed = 150f;

        public static readonly Vector3[] DetectedDirections = new Vector3[49];

        public static readonly Vector3 VectorRight80 = new(Mathf.Sin(80f * Mathf.Deg2Rad), 0f, Mathf.Cos(80f * Mathf.Deg2Rad));
        public static readonly Vector3 VectorUp80 = new(0f, Mathf.Sin(80f * Mathf.Deg2Rad), Mathf.Cos(80f * Mathf.Deg2Rad));

        static HumanAIController()
        {
            for (int i = -3; i <= 3; i++)
            {
                for (int j = -3; j <= 3; j++)
                {
                    int x = i + 3;
                    int y = j + 3;
                    int idx = y * 7 + x;
                    float xa = i * 30f * Mathf.Deg2Rad;
                    float ya = j * 30f * Mathf.Deg2Rad;
                    DetectedDirections[idx] = new Vector3(
                        Mathf.Sin(xa) * Mathf.Cos(ya),
                        Mathf.Sin(ya),
                        Mathf.Cos(xa) * Mathf.Cos(ya)
                    );
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _human = GetComponent<Human>();
            _human.Stats.Perks["OmniDash"].CurrPoints = 1;
            Automaton = new HumanAIAutomaton();
            Automaton.Init(this);
            Automaton.DefaultState = Automaton.GetState(HumanAIStates.Battle);
        }

        protected void UpdateMovementInput()
        {
            if (_human.Dead || _human.State == HumanState.Stun)
            {
                return;
            }
            _human.HoldJump = DoJump;
            _human.IsWalk = MoveDirection != null;
            if (_human.MountState != HumanMountState.Horse)
            {
                if (_human.Grounded && _human.State != HumanState.Idle)
                    return;
                if (!_human.Grounded && (_human.State == HumanState.EmoteAction || (_human.State == HumanState.SpecialAttack && _human.Special is not DownStrikeSpecial && _human.Special is not StockSpecial) ||
                    _human.Animation.IsPlaying("dash") || _human.Animation.IsPlaying("jump") || _human.IsFiringThunderspear()))
                    return;
            }
            _human.HoldLeft = false;
            _human.HoldRight = false;
            if (MoveDirection != null)
            {
                Vector3 moveDirection = (Vector3)MoveDirection;
                float cross = AimDirection.x * moveDirection.z - AimDirection.z * moveDirection.x;
                if (cross < 0)
                {
                    _human.HoldLeft = true;
                }
                else
                {
                    _human.HoldRight = true;
                }
                _character.TargetAngle = GetTargetAngle((Vector3)MoveDirection);
                _character.HasDirection = true;
                Vector3 v = new Vector3(moveDirection.x, 0f, moveDirection.z);
                float magnitude = (v.magnitude <= 0.95f) ? ((v.magnitude >= 0.25f) ? v.magnitude : 0f) : 1f;
                _human.TargetMagnitude = magnitude;
            }
            else
            {
                _character.HasDirection = false;
                _human.TargetMagnitude = 0f;
            }
        }

        void UpdateHookInput()
        {
            //TestScore();
            bool canHook = _human.State != HumanState.Grab && _human.State != HumanState.Stun && _human.Stats.CurrentGas > 0f
                && _human.MountState != HumanMountState.MapObject && !_human.Dead;
            _human.HoldHookLeft = DoHookLeft;
            _human.HoldHookRight = DoHookRight;
            _human.HoldHookBoth = false;
            // bool hasHook = _human.HookLeft.HasHook() || _human.HookRight.HasHook();
            if (_human.CancelHookLeftKey)
            {
                if (DoHookLeft)
                    DoHookLeft = false;
                else
                    _human.CancelHookLeftKey = false;
            }
            if (_human.CancelHookRightKey)
            {
                if (DoHookRight)
                    DoHookRight = false;
                else
                    _human.CancelHookRightKey = false;
            }
            _human.HookLeft.HookBoth = false;
            _human.HookRight.HookBoth = false;
            _human.HookLeft.SetInput(canHook && !IsSpin3Special() && DoHookLeft);
            _human.HookRight.SetInput(canHook && !IsSpin3Special() && DoHookRight);

            if (_human.Stats.CurrentGas <= 0f && (DoHookLeft || DoHookRight))
            {
                DoHookLeft = false;
                DoHookRight = false;
                _human.PlaySoundRPC(HumanSounds.NoGas, Util.CreateLocalPhotonInfo());
            }
        }

        private HashSet<HumanState> _illegalWeaponStates = new HashSet<HumanState>() { HumanState.Grab, HumanState.SpecialAction, HumanState.EmoteAction, HumanState.Reload,
            HumanState.SpecialAttack, HumanState.Stun };

        protected void UpdateActionInput()
        {
            // UpdateHookInput();
            // UpdateReelInput();
            //UpdateDashInput();
            bool canWeapon = _human.IsAttackableState && !_illegalWeaponStates.Contains(_human.State) && !_human.Dead;
            _human._gunArmAim = false;
            if (canWeapon)
            {
                if (_human.Weapon is AmmoWeapon && ((AmmoWeapon)_human.Weapon).RoundLeft == 0 &&
                    !(_human.Weapon is ThunderspearWeapon && ((ThunderspearWeapon)_human.Weapon).HasActiveProjectile()))
                {
                    if (DoAttack && _human.State == HumanState.Idle)
                        _human.Reload();
                }
                else
                {
                    _human.Weapon.SetInput(DoAttack);
                }
            }
            else
                _human.Weapon.SetInput(false);
            if (_human.Special != null)
            {
                bool canSpecial = _human.IsAttackableState &&
                    (_human.Special is EscapeSpecial || _human.Special is ShifterTransformSpecial || _human.State != HumanState.Grab) && _human.CarryState != HumanCarryState.Carry
                    && _human.State != HumanState.EmoteAction && _human.State != HumanState.Attack && _human.State != HumanState.SpecialAttack && !_human.Dead;
                bool canSpecialHold = _human.Special is BaseHoldAttackSpecial && _human.IsAttackableState && _human.State != HumanState.Grab && (_human.State != HumanState.Attack || _human.Special is StockSpecial) &&
                    _human.State != HumanState.EmoteAction && _human.State != HumanState.Grab && _human.CarryState != HumanCarryState.Carry && !_human.Dead;
                if (canSpecial || canSpecialHold)
                {
                    // Makes AHSSTwinShot activate on key up instead of key down
                    _human.Special.SetInput(DoSpecial);
                }
                else
                    _human.Special.SetInput(false);
            }
            if (_human.Dead || _human.State == HumanState.Stun)
                return;
            if (_human.MountState == HumanMountState.None)
            {
                if (_human.CanJump())
                {
                    if (DoJump)
                        _human.Jump();
                    else if (DoHorseMount && _human.Horse != null && _human.MountState == HumanMountState.None &&
                    Vector3.Distance(_human.Horse.Cache.Transform.position, _human.Cache.Transform.position) < 15f && !_human.HasDirection)
                        _human.MountHorse();
                    else if (DoDodge)
                    {
                        if (_human.HasDirection)
                            _human.Dodge(_human.TargetAngle + 180f);
                        else
                            _human.Dodge(_human.TargetAngle);
                    }
                }
                if (_human.State == HumanState.Idle)
                {
                    if (DoReload)
                        _human.Reload();
                }
                if (_human.CarryState == HumanCarryState.Carry)
                {
                    if (DoHorseMount)
                        _human.Cache.PhotonView.RPC("UncarryRPC", RpcTarget.All, new object[0]);
                }
            }
            else if (_human.MountState == HumanMountState.Horse)
            {
                if (DoHorseMount && _human.State == HumanState.Idle)
                    _human.Unmount(false);
                else if (DoJump)
                    _human.Horse.Jump();

                if (_human.State == HumanState.Idle && _human.IsAttackableState)
                {
                    if (DoReload)
                        _human.Reload();
                }
            }
        }

        void UpdateReelInput()
        {
            if (ReelAxis < 0)
            {
                _human.ReelInAxis = -1f;
            }
            if (ReelAxis > 0)
            {
                _human.ReelOutAxis = 1f;
            }
            else
            {
                _human.ReelOutAxis = 0f;
            }
        }

        void UpdateDashInput()
        {
            if (!_human.Grounded && _human.State != HumanState.AirDodge && _human.MountState == HumanMountState.None && _human.State != HumanState.Grab && _human.CarryState != HumanCarryState.Carry
                && _human.State != HumanState.Stun && _human.State != HumanState.EmoteAction && _human.State != HumanState.SpecialAction && !_human.Dead)
            {
                if (DashDirection != null)
                {
                    Vector3 direction = (Vector3)DashDirection;
                    if (_human.Stats.Perks["OmniDash"].CurrPoints == 1)
                    {
                        _human.DashVertical(GetTargetAngle(direction), direction);
                    }
                    else if (_human.Stats.Perks["VerticalDash"].CurrPoints == 1)
                    {
                        float angle = SceneLoader.CurrentCamera.Cache.Transform.rotation.eulerAngles.x;
                        if (angle < 0)
                            angle += 360f;
                        if (angle >= 360f)
                            angle -= 360f;
                        if (angle > 0f && angle < 180f)
                            _human.DashVertical(GetTargetAngle(direction), Vector3.down);
                        else
                            _human.DashVertical(GetTargetAngle(direction), Vector3.up);
                    }
                    else
                    {
                        _human.Dash(GetTargetAngle(direction));
                    }
                }
            }
        }

        bool IsSpin3Special()
        {
            return _human.State == HumanState.SpecialAttack && _human.Special is Spin3Special;
        }

        void DefaultAction()
        {
            MoveDirection = null;
            SetAimDirection(null);
            DashDirection = null;
            DoHorseMount = false;
            DoJump = false;
            DoAttack = false;
            DoSpecial = false;
            DoHorseMount = false;
            DoDodge = false;
            DoReload = false;
            ReelAxis = 0;
            if (Target != null)
            {
                if (Target is BaseTitan titan)
                {
                    var napeBox = (CapsuleCollider)titan.BaseTitanCache.NapeHurtbox;
                    var globalScale = napeBox.transform.lossyScale;
                    var radius = napeBox.radius * Mathf.Max(globalScale.x, globalScale.y);
                    TargetPosition = napeBox.transform.position - 1.5f * radius * napeBox.transform.forward + 0.5f * radius * napeBox.transform.up;
                    titan.TitanColliderToggler.RegisterLook();
                }
                else
                {
                    TargetPosition = Target.GetPosition();
                }
                TargetDirection = TargetPosition - _human.Cache.Transform.position;
            }
            else
            {
                ReleaseHookAll();
            }
        }

        void ResetAction()
        {
            DefaultAction();
            DoHookLeft = false;
            DoHookRight = false;
        }

        public void Attack()
        {
            DoAttack = true;
        }

        public void Reload()
        {
            DoReload = true;
        }

        public bool NeedReload()
        {
            if (_human.Weapon is BladeWeapon bladeWeapon)
            {
                return bladeWeapon.CurrentDurability <= 0f;
            }
            else if (_human.Weapon is AmmoWeapon ammoWeapon && !(_human.Weapon is ThunderspearWeapon && SettingsManager.InGameCurrent.Misc.ThunderspearPVP.Value))
            {
                return ammoWeapon.RoundLeft <= 0f;
            }
            return false;
        }

        public bool NeedRefill()
        {
            if (_human.Weapon is BladeWeapon bladeWeapon)
            {
                return bladeWeapon.CurrentDurability <= 0f && bladeWeapon.BladesLeft <= 0;
            }
            else if (_human.Weapon is AmmoWeapon ammoWeapon && !(_human.Weapon is ThunderspearWeapon && SettingsManager.InGameCurrent.Misc.ThunderspearPVP.Value))
            {
                return ammoWeapon.RoundLeft <= 0f && ammoWeapon.AmmoLeft <= 0f;
            }
            return false;
        }

        public void Special()
        {
            DoSpecial = true;
        }

        public void SetAimDirection(Vector3? direction)
        {
            if (direction != null)
            {
                AimDirection = (Vector3)direction;
                _human.CustomAimPoint = _human.transform.position + 10 * AimDirection;
            }
            else
            {
                AimDirection = Vector3.zero;
                _human.CustomAimPoint = null;
            }

        }

        public void SetAimPoint(Vector3? position)
        {
            if (position != null)
            {
                _human.CustomAimPoint = position;
                AimDirection = (Vector3)position - _human.transform.position;
            }
            else
            {
                AimDirection = Vector3.zero;
                _human.CustomAimPoint = null;
            }
        }

        public void Move(Vector3 direction)
        {
            MoveDirection = direction;
        }

        public void Jump()
        {
            DoJump = true;
        }

        public void Dodge(Vector3 direction)
        {
            MoveDirection = direction;
            DashDirection = direction;
            DoDodge = true;
            UpdateDashInput();
        }

        public void Reel(int reelAxis)
        {
            ReelAxis = reelAxis;
            UpdateReelInput();
        }

        public void ReelIn()
        {
            Reel(-1);
        }

        public void ReelOut()
        {
            if (ReelAxis >= 0)
            {
                Reel(1);
            }
        }

        public bool LaunchHookLeft(Vector3 position)
        {
            if (_hookLeftTimer <= 0f && _human.HookLeft.HookReady())
            {
                _hookLeftTimer = 0.8f;
                SetAimPoint(position);
                DoHookLeft = true;
                DoHookLeftHooked = false;
                UpdateHookInput();
                return true;
            }
            return false;
        }

        public bool LaunchHookRight(Vector3 position)
        {
            if (_human.HookRight.HookReady())
            {
                _hookRightTimer = 0.8f;
                SetAimPoint(position);
                DoHookRight = true;
                DoHookRightHooked = false;
                UpdateHookInput();
                return true;
            }
            return false;
        }

        public bool LaunchHook(Vector3 position)
        {
            if (LaunchHookLeft(position))
            {

                return true;
            }
            return LaunchHookRight(position);
        }

        public void ReleaseHookLeft()
        {
            DoHookLeft = false;
            DoHookLeftHooked = false;
            UpdateHookInput();
        }

        public void ReleaseHookRight()
        {
            DoHookRight = false;
            DoHookRightHooked = false;
            UpdateHookInput();
        }

        public void ReleaseHookAll()
        {
            DoHookLeft = false;
            DoHookRight = false;
            DoHookLeftHooked = false;
            DoHookRightHooked = false;
            UpdateHookInput();
        }

        public bool IsHookedTarget(HookUseable hook, bool needNape = false, bool fuzzy = false, float distannse2TargetTol = 5.0f)
        {
            if (hook.IsHooked() && Target != null)
            {
                if (Target is MapTargetable || fuzzy)
                {
                    return Vector3.Distance(Target.GetPosition(), hook.GetHookPosition()) < distannse2TargetTol;
                }
                else if (Target is BaseCharacter character)
                {
                    if (needNape && Target is BaseTitan titan)
                    {
                        return Vector3.Distance(titan.BaseTitanCache.NapeHurtbox.transform.position, hook.GetHookPosition()) < distannse2TargetTol;
                    }
                    else
                    {
                        return hook.GetHookCharacter() == character;
                    }
                }
            }
            return false;
        }

        public ITargetable FindNearestEnemy()
        {
            Vector3 position = _human.Cache.Transform.position;
            float nearestDistance = float.PositiveInfinity;
            ITargetable nearestCharacter = null;
            var character = _human.Detection.ClosestEnemy;
            if (character != null && !character.Dead)
            {
                float distance = Vector3.Distance(character.Cache.Transform.position, position);
                if (distance < nearestDistance && distance < DetectRange)
                {
                    nearestDistance = distance;
                    nearestCharacter = character;
                }
            }
            foreach (MapTargetable targetable in MapLoader.MapTargetables)
            {
                if (targetable == null || !targetable.ValidTarget())
                    continue;
                float distance = Vector3.Distance(targetable.GetPosition(), position);
                if (distance < nearestDistance && distance < DetectRange)
                {
                    nearestDistance = distance;
                    nearestCharacter = targetable;
                }
            }
            return nearestCharacter;
        }

        protected void FixedUpdateTargetStatus()
        {
            if (Target != null)
            {
                var targetCurrentPosition = Target.GetPosition();
                if (_targetLastPosition is Vector3 p)
                {
                    TargetVelocity = (targetCurrentPosition - p) / Time.deltaTime;
                }
                else
                {
                    TargetVelocity = Vector3.zero;
                }
                _targetLastPosition = targetCurrentPosition;
            }
            else
            {
                TargetVelocity = Vector3.zero;
                _targetLastPosition = null;
            }
        }

        void AfterAction()
        {
            if (Target != null)
            {
                SetAimPoint(TargetPosition);
            }
        }


        protected override void FixedUpdate()
        {
            FixedUpdateTargetStatus();
            DefaultAction();
            if (_hookLeftTimer > 0f)
            {
                _hookLeftTimer -= Time.deltaTime;
                if (_human.HookLeft.IsHooked())
                {
                    DoHookLeftHooked = true;
                }
                if (_hookLeftTimer <= 0 && !DoHookLeftHooked)
                {
                    ReleaseHookLeft();
                }
            }
            if (_hookRightTimer > 0f)
            {
                _hookRightTimer -= Time.deltaTime;
                if (_human.HookRight.IsHooked())
                {
                    DoHookRightHooked = true;
                }
                if (_hookRightTimer <= 0 && !DoHookRightHooked)
                {
                    ReleaseHookRight();
                }
            }
            Automaton.Action();
            AfterAction();

            // FixedUpdate is enough for ai
            if (!_human.FinishSetup)
                return;
            UpdateMovementInput();
            UpdateActionInput();
        }

        public bool IsTargetValid()
        {
            if (Target == null)
            {
                return false;
            }

            if (Target is BaseCharacter target)
            {
                if (target.Dead)
                {
                    Target = null;
                    return false;
                }
            }
            return Target.ValidTarget();
        }

        public void JumpTo(Vector3 position)
        {
            var humanPosition = _human.Cache.Transform.position;
            var direction = position - humanPosition;
            direction.y = 0;
            Move(direction.normalized);
            Jump();
        }

        public static Vector3 GetDetectedDirection(int xa, int ya)
        {
            return DetectedDirections[(ya + 3) * 7 + xa + 3];
        }

        public void RandomJump(bool actionInAir, float forward)
        {
            if (_human.Grounded)
            {
                var randAngle = Random.Range(-80f, 80f) * Mathf.Deg2Rad;
                var randomDirection = new Vector3(Mathf.Sin(randAngle), 0f, Mathf.Cos(randAngle)) * forward;
                var q = Quaternion.LookRotation(new Vector3(TargetDirection.x, 0f, TargetDirection.z).normalized);
                Move(q * randomDirection);
                Jump();
            }
            else if (actionInAir)
            {
                RaycastHit result;
                var isHit = Physics.Linecast(_human.transform.position + new Vector3(0f, 0.1f, 0f), _human.transform.position + new Vector3(0f, -10f, 0f), out result, BarrierMask);
                if (isHit && result.distance < 7)
                {
                    var velocity = _human.Cache.Rigidbody.velocity;
                    velocity.y = 0;
                    velocity = velocity.normalized;
                    var targetDirection = new Vector3(TargetDirection.x, 0f, TargetDirection.z).normalized;
                    var angle = Vector3.Angle(velocity, targetDirection);
                    var randAngle = Random.Range(0f, 80f) * Mathf.Deg2Rad;
                    var v = Vector3.RotateTowards(velocity, targetDirection, randAngle, 0f);
                    Move(v);
                }
                else
                {
                    var velocity = _human.Cache.Rigidbody.velocity;
                    velocity = velocity.normalized;
                    var targetDirection = TargetDirection.normalized;
                    var angle = Vector3.Angle(velocity, targetDirection);
                    var randAngle = Random.Range(0f, 80f) * Mathf.Deg2Rad;
                    var v = Vector3.RotateTowards(velocity, targetDirection, randAngle, 0f);
                    Dodge(v);
                }
            }
        }

        public Vector3 GetHookPosition()
        {
            Vector3 hookPosition = Vector3.zero;
            if (_human.IsHookedAny())
            {
                if (!_human.IsHookedLeft()) // HookedRight
                {
                    hookPosition = _human.HookRight.GetHookPosition();
                }
                else if (!_human.IsHookedRight())
                {
                    hookPosition = _human.HookLeft.GetHookPosition();
                }
                else
                {
                    hookPosition = 0.5f * (_human.HookLeft.GetHookPosition() + _human.HookRight.GetHookPosition());
                }
            }
            return hookPosition;
        }


        public void StraightFlight(Vector3 position, float tolAngle, bool useDash = false, float keepDistance = 5f)
        {
            var humanPosition = _human.transform.position;
            var direction = position - humanPosition;
            var velocity = _human.Cache.Rigidbody.velocity;
            var hitBarrier = keepDistance > 0.1f && Physics.Linecast(humanPosition, humanPosition + direction.normalized * keepDistance, MapMask);
            var hitBarrier2 = keepDistance > 0.1f && Physics.Linecast(humanPosition, humanPosition + direction.normalized * (keepDistance + 5f), MapMask);
            if (hitBarrier2)
            {
                Move(-direction.normalized);
            }
            else
            {
                Move(direction.normalized);
            }
            Jump();
            if (_human.HasHook() || Vector3.Angle(direction, velocity) < tolAngle)
            {
                if (_human.IsHookedAny() && !hitBarrier)
                {
                    ReelOut();
                }
            }
            if (_human.IsHookedAny())
            {
                if (_human.Cache.Rigidbody.velocity.magnitude < 0.5f)
                {
                    ReleaseHookAll();
                }
                else
                {
                    var hookPosition = GetHookPosition();
                    var distance2hook = Vector3.Distance(hookPosition, humanPosition);
                    if (distance2hook > 5f)
                    {
                        if (_human.Pivot && !hitBarrier)
                        {
                            var reelInVelocity = CalcReelVelocity(_human.transform.position, hookPosition, velocity, -1f);

                            if (Vector3.Angle(direction, reelInVelocity) < tolAngle)
                            {
                                ReelIn();
                                return;
                            }
                            ReleaseHookAll();
                        }
                    }

                    if (hookPosition.y < humanPosition.y && hookPosition.y < position.y)
                    {
                        ReleaseHookAll();
                    }
                }
            }
            if (!_human.HasHook() && !hitBarrier)
            {
                if (useDash && Vector3.Angle(direction, velocity) <= 90)
                {
                    Dodge(direction.normalized);
                }
                var directionH = new Vector3(direction.x, 0f, direction.z);
                var directionQuaternion = Quaternion.LookRotation(directionH.normalized);
                //TODO

                for (int i = 1; i >= -1; i--)
                {
                    var randomAngle = Random.Range(30.0f, 80.0f) * RandomGen.GetRandomSign() * Mathf.Deg2Rad;
                    var rawRandomDirection = new Vector3(Mathf.Sin(randomAngle), 0f, Mathf.Cos(randomAngle));
                    rawRandomDirection *= Mathf.Cos(i * 30f * Mathf.Deg2Rad);
                    rawRandomDirection.y = Mathf.Sin(i * 30f * Mathf.Deg2Rad);
                    var randomDirection = directionQuaternion * rawRandomDirection;
                    var hookPosition = humanPosition + randomDirection * 115f;
                    if (Physics.Linecast(humanPosition + randomDirection.normalized * 2f, hookPosition, BarrierMask))
                    {
                        LaunchHook(hookPosition);
                        break;
                    }
                }

                // Debug.DrawLine(humanPosition, hookPosition, Color.blue);
            }
        }

        public void FlightAround(Vector3 position, float radius, float safeRadius, float hookTolH = 5f, float hookTolY = 1f)
        {
            var hookPosition = GetHookPosition();
            var humanPosition = _human.transform.position;
            var direction = position - humanPosition;
            var directionH = new Vector3(direction.x, 0f, direction.z);
            var velocity = _human.Cache.Rigidbody.velocity;
            var velocityH = new Vector3(velocity.x, 0f, velocity.z);
            var clockwise = Vector3.SignedAngle(velocityH, directionH, Vector3.up) > 0;

            var rotSign = clockwise ? -1f : 1f;
            var dirH = new Vector3(direction.x, 0f, direction.z);
            var rotDir = new Vector3(dirH.z, 0f, -dirH.x) * rotSign;

            var hook2human = Vector3.Distance(hookPosition, _human.transform.position);
            Move(rotDir.normalized);
            Jump();
            var hookDiff = hookPosition - position;
            var hookDiffH = new Vector2(hookDiff.x, hookDiff.z).magnitude;
            var hookDiffY = Mathf.Abs(hookDiff.y);
            var isValidVelocity = velocity.magnitude > 0.1f && (Vector3.Angle(rotDir, velocityH) < 30f || Vector3.Angle(direction, velocity) < 30f);
            var unbalance = Math.Abs(direction.y) < 1f && Vector3.Angle(velocity, Vector3.up) > 20f;
            // Debug.Log(Vector3.Angle(rotDir, velocityH) + " valid angle " + Vector3.Angle(direction, velocity));
            if (_human.IsHookedAny() && hookDiffH < hookTolH && hookDiffY < hookTolY && isValidVelocity && !unbalance)
            {
                if (hook2human < radius - 0.5f)
                {
                    // Debug.Log(hook2human + " : " + radius + " reelaxis " + ReelAxis);
                    ReelOut();
                    // Debug.Log(" reelaxis " + ReelAxis);
                }
                else if (hook2human > safeRadius * 2)
                {
                    ReelIn();
                }
            }
            else if (!_human.HasHook())
            {
                hookPosition = humanPosition + direction.normalized * 115f;
                if (directionH.magnitude > safeRadius && Physics.Linecast(humanPosition, hookPosition, out RaycastHit result, BarrierMask))
                {
                    if (result.distance > 5f)
                    {
                        LaunchHook(hookPosition);
                    }
                }
                else
                {
                    Move((-dirH).normalized);
                }
            }
            else if (_human.IsHookedAny())
            {
                if (_human.IsHookedAny() && (hookDiffH > hookTolH || hookDiffY > hookTolY || hook2human < safeRadius || unbalance))
                {
                    ReleaseHookAll();
                }
                else if (direction.magnitude > radius && _human.Pivot)
                {
                    ReelIn();
                }
            }
        }

        public bool DetectDirection(Vector3 direction, float startOffset,
        float endOffset, out RaycastHit result)
        {
            //Tips: Get GlobalDirection: self.Core.TransformDirection(localDirection)
            var start = Human.Cache.Transform.position + direction.normalized * startOffset;
            var end = Human.Cache.Transform.position + direction.normalized * (DetectRange + endOffset);
            return Physics.Linecast(start, end, out result, MapMask);
        }

        /*
        targetPosition : World Position of Target;
        hookPosition: world position of hook. refer to pivot;
        velocity: character velocity;
        attackDistance: max atack distance
        predictedLaps: #Laps to predict if doing the pivot movement. e.g. 0.4 mean predict 0.4 laps
        predictDense: #points to predict;
        return second left to activate hitbox, If the best time is missed, it will return negative.
        */
        public static float PredictAttackOpportunity(Vector3 start, Vector3 targetPosition, Vector3 velocity, Vector3 hookPosition, float attackDistance, float predictedLaps, float predictDense)
        {
            if (velocity.magnitude <= 0.1)
            {
                return Mathf.Infinity;
            }
            var targetDirection = targetPosition - start;
            var targetProject = Vector3.Project(targetDirection, velocity);
            var closestDirection = targetDirection - targetProject;
            var closestDistance = closestDirection.magnitude;
            var distance2Closest = targetProject.magnitude;
            if (hookPosition != null)
            {
                var hookDirection = hookPosition - start;
                var hookProject = Vector3.Project(hookDirection, velocity);
                var radiusDirection = hookDirection - hookProject;
                var radius = radiusDirection.magnitude;
                var shortestDistance2Lap = hookProject.magnitude;
                if (distance2Closest > shortestDistance2Lap)
                {
                    var lapT = Mathf.Infinity;
                    predictDense += 1;
                    var T = 2 * Mathf.PI * radius / velocity.magnitude;
                    var i = 1f;
                    while (i < predictDense)
                    {
                        var N = i / predictDense * predictedLaps;
                        var pos = CalcRoundPoints(start, hookPosition, velocity, N);
                        var distance = Vector3.Distance(pos, targetPosition);
                        if (distance < attackDistance)
                        {
                            lapT = N * T;
                            i = predictDense;
                        }
                        i += 1;
                    }
                    var t0 = shortestDistance2Lap / velocity.magnitude;
                    return lapT + t0;
                }
            }

            // Debug.Log("closestDistance: " + closestDistance + " attackDistance: " + attackDistance);
            if (closestDistance < attackDistance && Vector3.Angle(targetProject, velocity) <= 90)
            {
                // About advanceDistance: The attackDistance is greater than the sqrt(closestDistance^2, advanceDistance^2).
                var advanceDistance = attackDistance - closestDistance;
                return (distance2Closest - advanceDistance) / velocity.magnitude;
            }
            return Mathf.Infinity;
        }

        /*
    return radius vector that start from human and points at hookPosition
    */
        public static Vector3 CalcCircleRadius(Vector3 start, Vector3 hookPosition, Vector3 velocity)
        {
            return ShortestDistance(hookPosition - start, velocity);
        }

        /*
        return shotest distance from point a to direction b
        */
        public static Vector3 ShortestDistance(Vector3 a, Vector3 b)
        {
            return a - Vector3.Project(a, b);
        }

        public static Vector3 CorrectShootPosition(Vector3 start, Vector3 shootPosition, Vector3 targetVelocity, float shootSpeed)
        {
            if (targetVelocity.magnitude < 0.1f)
            {
                return shootPosition;
            }
            var shootDistance = Vector3.Distance(shootPosition, start);
            float t = shootDistance / shootSpeed;
            return shootPosition + t * targetVelocity;
        }

        public static Vector3 CalcReelVelocity(Vector3 start, Vector3 hookPosition, Vector3 velocity, float reelAxis)
        {
            float addSpeed = 0.1f;
            float newSpeed = velocity.magnitude + addSpeed;
            var v = hookPosition - (start - new Vector3(0f, 0.02f, 0f));
            float reel = Mathf.Clamp(reelAxis, -0.8f, 0.8f) + 1f;
            v = Vector3.RotateTowards(v, velocity, 1.53938f * reel, 1.53938f * reel).normalized;
            return v * newSpeed;
        }
        /*
        N: Rotation period;
        return End Position;
        */
        public static Vector3 CalcRoundPoints(Vector3 start, Vector3 hookPosition, Vector3 velocity, float N)
        {
            var CP = start - hookPosition;
            var rotationAxis = Vector3.Cross(CP, velocity).normalized;
            // var  speedMagnitude = V.magnitude;
            // var  radius = CP.magnitude;
            // var angularSpeed = speedMagnitude / radius;
            var circle = 2 * math.PI * N;
            return hookPosition + Mathf.Cos(circle) * CP + Mathf.Sin(circle) * Vector3.Cross(rotationAxis, CP) + (1 - Mathf.Cos(circle)) * Vector3.Dot(rotationAxis, CP) * rotationAxis;
        }

        public bool IsDirectlySeeingTarget(RaycastHit result, float tolDis = 5f)
        {
            var point = result.point;
            var target2Point = point - TargetPosition;
            return target2Point.magnitude <= tolDis;
        }

        public Vector3? FindTempTarget(RaycastHit result, float lookTargetTolDis = 5f, float barrierTolDis = 50f)
        {
            var humanPosition = _human.Cache.Transform.position;
            var point = result.point;
            var self2Point = point - humanPosition;
            if (IsDirectlySeeingTarget(result, lookTargetTolDis))
            {
                // Move to target;
                return null;
            }
            else if (self2Point.magnitude <= barrierTolDis)
            {
                int[] directionOrderV;
                int[] directionOrderH = new int[] { 0, -1, 1, -2, 2, -3, 3 };
                if (TargetDirection.y > 0)
                {
                    directionOrderV = new int[] { 1, 2, 3, 0, -1, -2, -3 };
                }
                else
                {
                    directionOrderV = new int[] { 0, 1, -1, 2, -2, 3, -3 };
                }
                // Adjust the body position.
                var directionQuaternion = Quaternion.LookRotation(new Vector3(TargetDirection.x, 0.0f, TargetDirection.z));
                foreach (var dirV in directionOrderV)
                {
                    foreach (var dirH in directionOrderH)
                    {
                        var vRand = GetDetectedDirection(dirH, dirV);
                        var direction = (directionQuaternion * vRand).normalized;
                        var isHit = DetectDirection(direction, 0f, 5f, out result);
                        if (!isHit || (result.distance + 5f >= 40f))
                        {
                            return humanPosition + 35f * direction;
                        }
                    }
                }
            }
            // Move Closest;
            return (point + humanPosition) * 0.5f;
        }
    }
}
