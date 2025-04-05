using UnityEngine;
using Unity.Mathematics;
using ApplicationManagers;
using Settings;
using Characters;
using UI;
using System.Collections.Generic;
using Utility;
using Photon.Pun;
using UnityEngine.EventSystems;
using Map;
using Random = UnityEngine.Random;
using Discord;
using UnityEditor.UI;
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
        public bool DoHookRight = false;

        public bool DoHookBoth = false;

        public float _hookLeftTimer = 0f;
        public float _hookRightTimer = 0f;

        public HumanAIAutomaton Automaton;

        public ITargetable Target;

        public Vector3 TargetPosition;
        public Vector3 TargetDirection;

        public float LockingDistance = 100f;

        public float DetectRange = 10000f;

        public float HookSpeed = 150f;

        public static readonly Vector3 VectorLeft80 = new(Mathf.Sin(-80f * Mathf.Deg2Rad), 0f, Mathf.Cos(-80f * Mathf.Deg2Rad));
        public static readonly Vector3 VectorRight80 = new(Mathf.Sin(80f * Mathf.Deg2Rad), 0f, Mathf.Cos(80f * Mathf.Deg2Rad));
        public static readonly Vector3 VectorUp80 = new(0f, Mathf.Sin(80f * Mathf.Deg2Rad), Mathf.Cos(80f * Mathf.Deg2Rad));


        protected override void Awake()
        {
            base.Awake();
            _human = GetComponent<Human>();
            _human.Stats.Perks["OmniDash"].CurrPoints = 1;
            Automaton = new HumanAIAutomaton();
            Automaton.Init(this);
            Automaton.DefaultState = Automaton.GetState(HumanAIStates.Battle);
            // _humanInput = SettingsManager.InputSettings.Human;
        }


        protected void Update()
        {
            if (!_human.FinishSetup)
                return;
            UpdateMovementInput();
            UpdateActionInput();
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
            bool hookBoth = DoHookBoth;
            bool hookLeft = DoHookLeft;
            bool hookRight = DoHookRight;
            _human.HoldHookLeft = hookLeft;
            _human.HoldHookRight = hookRight;
            _human.HoldHookBoth = hookBoth;
            bool hasHook = _human.HookLeft.HasHook() || _human.HookRight.HasHook();
            if (_human.CancelHookBothKey)
            {
                if (hookBoth)
                    hookBoth = false;
                else
                    _human.CancelHookBothKey = false;
            }
            if (_human.CancelHookLeftKey)
            {
                if (hookLeft)
                    hookLeft = false;
                else
                    _human.CancelHookLeftKey = false;
            }
            if (_human.CancelHookRightKey)
            {
                if (hookRight)
                    hookRight = false;
                else
                    _human.CancelHookRightKey = false;
            }
            _human.HookLeft.HookBoth = hookBoth && !hookLeft;
            _human.HookRight.HookBoth = hookBoth && !hookRight;
            _human.HookLeft.SetInput(canHook && !IsSpin3Special() && (hookLeft || (hookBoth && (_human.HookLeft.IsHooked() || !hasHook))));
            _human.HookRight.SetInput(canHook && !IsSpin3Special() && (hookRight || (hookBoth && (_human.HookRight.IsHooked() || !hasHook))));

            if (_human.Stats.CurrentGas <= 0f && (hookLeft || hookRight || hookBoth))
            {
                if (DoHookLeft || DoHookRight || DoHookBoth)
                {
                    DoHookLeft = false;
                    DoHookRight = false;
                    DoHookBoth = false;
                    _human.PlaySoundRPC(HumanSounds.NoGas, Util.CreateLocalPhotonInfo());
                }
            }
        }

        private HashSet<HumanState> _illegalWeaponStates = new HashSet<HumanState>() { HumanState.Grab, HumanState.SpecialAction, HumanState.EmoteAction, HumanState.Reload,
            HumanState.SpecialAttack, HumanState.Stun };

        protected void UpdateActionInput()
        {
            UpdateHookInput();
            UpdateReelInput();
            UpdateDashInput();
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
                if (!_human._reelInWaitForRelease)
                    _human.ReelInAxis = -1f;
            }
            if (ReelAxis > 0)
            {
                _human.ReelOutAxis = 1f;
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
                    TargetPosition = titan.BaseTitanCache.NapeHurtbox.transform.position;
                }
                else
                {
                    TargetPosition = Target.GetPosition();
                }
                TargetDirection = TargetPosition - _human.Cache.Transform.position;
            }
        }

        void ResetAction()
        {
            DefaultAction();
            DoHookLeft = false;
            DoHookRight = false;
            DoHookBoth = false;
        }

        public void Attack()
        {
            DoAttack = true;
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
        }

        public void Reel(int reelAxis)
        {
            ReelAxis = reelAxis;
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
            if (_human.HookLeft.HookReady())
            {
                SetAimPoint(position);
                DoHookLeft = true;
                UpdateHookInput();
                return true;
            }
            return false;
        }

        public bool LaunchHookRight(Vector3 position)
        {
            if (_human.HookRight.HookReady())
            {
                SetAimPoint(position);
                DoHookRight = true;
                UpdateHookInput();
                return true;
            }
            return false;
        }

        public bool LaunchHook(Vector3 position)
        {
            if (_hookLeftTimer <= 0 && _human.HookLeft.HookReady())
            {
                _hookLeftTimer = 0.8f;
                return LaunchHookLeft(position);
            }
            else if (_hookRightTimer <= 0 && _human.HookRight.HookReady())
            {
                _hookRightTimer = 0.8f;
                return LaunchHookRight(position);
            }
            return false;
        }

        public void ReleaseHookLeft()
        {
            DoHookLeft = false;
        }

        public void ReleaseHookRight()
        {
            DoHookRight = false;
        }

        public void ReleaseHookAll()
        {
            DoHookLeft = false;
            DoHookRight = false;
        }

        public bool IsHookedTarget(HookUseable hook, bool needNape = false)
        {
            if (hook.IsHooked() && Target != null)
            {
                if (Target is MapTargetable target)
                {
                    return Vector3.Distance(target.GetPosition(), hook.GetHookPosition()) < 10.0f;
                }
                else if (Target is BaseCharacter character)
                {
                    if (needNape && Target is BaseTitan titan)
                    {
                        return Vector3.Distance(titan.BaseTitanCache.NapeHurtbox.transform.position, hook.GetHookPosition()) < 6.0f;
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


        protected override void FixedUpdate()
        {
            DefaultAction();
            _hookLeftTimer -= Time.deltaTime;
            _hookRightTimer -= Time.deltaTime;
            Automaton.Action();
        }

        public bool IsTargetValid()
        {
            if (Target == null)
            {
                return false;
            }

            if (Target is BaseCharacter target)
            {
                if (target.CurrentHealth <= 0)
                {
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


        public void StraightFlight(Vector3 position, float tolAngle, bool useDash=false)
        {
            if (_human.Grounded)
            {
                JumpTo(position);
                ReleaseHookAll();
                return;
            }
            Jump();
            var humanPosition = _human.transform.position;
            var direction = position - humanPosition;
            var velocity = _human.Cache.Rigidbody.velocity;
            if (_human.HasHook() || Vector3.Angle(direction, velocity) < tolAngle)
            {
                Move(direction.normalized);
                if (_human.IsHookedAny())
                {
                    ReelOut();
                }
            }
            if (_human.IsHookedAny())
            {
                var hookPosition = GetHookPosition();
                var distance2hook = Vector3.Distance(hookPosition, humanPosition);
                if (distance2hook > 5.0)
                {
                    if (_human.Pivot)
                    {
                        var reelInVelocity = CalcReelVelocity(_human.transform.position, hookPosition, velocity, -1f);
                        // var isHit = Physics.Linecast(humanPosition, humanPosition + reelInVelocity.normalized * direction.magnitude, BarrierMask);
                        // if (!isHit && Vector3.Angle(direction, reelInVelocity) < tolAngle)
                        if (Vector3.Angle(direction, reelInVelocity) < tolAngle)
                        {
                            ReelIn();
                            return;
                        }
                        ReleaseHookAll();
                    }
                }
                else
                {
                    ReleaseHookAll();
                }
            }

            if (!_human.HasHook())
            {
                if (useDash && Vector3.Angle(direction, velocity) <= 90)
                {
                    Dodge(direction.normalized);
                }
                var directionQuaternion = Quaternion.LookRotation(direction.normalized);
                var randomAngle = Random.Range(16.0f, 45.0f) * RandomGen.GetRandomSign() * Mathf.Deg2Rad;
                var randomDirection = new Vector3(Mathf.Sin(randomAngle), 0f, Mathf.Cos(randomAngle));
                randomDirection = directionQuaternion * randomDirection;
                var hookPosition = humanPosition + randomDirection * 115f;
                if (Physics.Linecast(humanPosition + randomDirection.normalized * 5f, hookPosition, BarrierMask))
                {
                    LaunchHook(hookPosition);
                }
            }
        }

        public bool DetectDirection(Vector3 direction, float startOffset,
        float endOffset, out RaycastHit result)
        {
            //Tips: Get GlobalDirection: self.Core.TransformDirection(localDirection)
            var start = Human.Cache.Transform.position + direction.normalized * startOffset;
            var end = Human.Cache.Transform.position + direction.normalized * (DetectRange + endOffset);
            return Physics.Linecast(start, end, out result, BarrierMask);
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

        public static Vector3 CorrectHookPosition(Vector3 start, Vector3 hookPosition, Vector3 targetVelocity, float hookSpeed)
        {
            if (targetVelocity.magnitude < 0.1f)
            {
                return hookPosition;
            }
            var hookingDistance = Vector3.Distance(hookPosition, start);
            float t = hookingDistance / hookSpeed;
            return hookPosition + t * targetVelocity;
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
    }
}
