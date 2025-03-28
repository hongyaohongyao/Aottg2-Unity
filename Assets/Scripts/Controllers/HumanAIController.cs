using UnityEngine;
using ApplicationManagers;
using Settings;
using Characters;
using UI;
using System.Collections.Generic;
using Utility;
using Photon.Pun;
using UnityEngine.EventSystems;

namespace Controllers
{
    class HumanAIController : BaseAIController
    {
        protected Human _human;
        protected float _reelOutScrollTimeLeft;
        protected float _reelInScrollCooldownLeft = 0f;
        protected float _reelInScrollCooldown = 0.2f;
        protected HumanInputSettings _humanInput;
        protected static LayerMask HookMask = PhysicsLayer.GetMask(PhysicsLayer.TitanMovebox, PhysicsLayer.TitanPushbox,
            PhysicsLayer.MapObjectEntities, PhysicsLayer.MapObjectProjectiles, PhysicsLayer.MapObjectAll);

        public Vector3? MoveDirection = null;

        public Vector3 AimDirection;

        public Vector3? DashDirection = null;

        public bool DoJump = false;

        public bool DoAttack = false; 
        public bool DoSpecial = false;

        public bool DoHorseMount = false;

        public bool DoDodge = false;

        public bool DoReload = false;

        public float ReelAxis = 0.0f;

        public bool DoHookLeft = false;
        public bool DoHookRight = false;

        public bool DoHookBoth = false;


        protected override void Awake()
        {
            base.Awake();
            _human = GetComponent<Human>();
            _humanInput = SettingsManager.InputSettings.Human;
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
            bool canWeapon =  _human.IsAttackableState && !_illegalWeaponStates.Contains(_human.State) && !_human.Dead;
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
            _reelOutScrollTimeLeft -= Time.deltaTime;
            if (_reelOutScrollTimeLeft <= 0f)
                _human.ReelOutAxis = 0f;
            if (_humanInput.ReelIn.GetKey())
            {
                if (!_human._reelInWaitForRelease)
                    _human.ReelInAxis = -1f;
                _reelInScrollCooldownLeft = _reelInScrollCooldown;
            }
            else
            {
                bool hasScroll = false;
                _reelInScrollCooldownLeft -= Time.deltaTime;
                foreach (InputKey inputKey in _humanInput.ReelIn.InputKeys)
                {
                    if (inputKey.IsWheel())
                        hasScroll = true;
                }
                foreach (InputKey inputKey in _humanInput.ReelIn.InputKeys)
                {
                    if (inputKey.IsWheel())
                    {
                        if (_reelInScrollCooldownLeft <= 0f)
                            _human._reelInWaitForRelease = false;
                    }
                    else
                    {
                        if (!hasScroll || inputKey.GetKeyUp())
                            _human._reelInWaitForRelease = false;
                    }
                }
            }
            foreach (InputKey inputKey in _humanInput.ReelOut.InputKeys)
            {
                if (inputKey.GetKey())
                {
                    _human.ReelOutAxis = 1f;
                    if (inputKey.IsWheel())
                        _reelOutScrollTimeLeft = SettingsManager.InputSettings.Human.ReelOutScrollSmoothing.Value;
                }
            }
        }

        void UpdateDashInput()
        {
            if (!_human.Grounded && _human.State != HumanState.AirDodge && _human.MountState == HumanMountState.None && _human.State != HumanState.Grab && _human.CarryState != HumanCarryState.Carry
                && _human.State != HumanState.Stun && _human.State != HumanState.EmoteAction && _human.State != HumanState.SpecialAction && !_human.Dead)
            {
                if (DashDirection!=null)
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
    }
}
