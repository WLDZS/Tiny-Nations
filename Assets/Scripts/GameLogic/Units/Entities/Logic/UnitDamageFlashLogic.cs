using BorFramework;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitDamageFlashLogic : Logic
    {
        private const float FullFlashDurationSeconds = 0.04f;
        private const float FadeDurationSeconds = 0.06f;
        private const float TotalFlashDurationSeconds =
            FullFlashDurationSeconds + FadeDurationSeconds;

        private static readonly int FlashAmountId =
            Shader.PropertyToID("_FlashAmount");

        private readonly UnitViewComp _view;
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private float _remainingSeconds;
        private bool _hasFlashOverride;

        public override ELogicPhase Phase => ELogicPhase.Presentation;

        public UnitDamageFlashLogic(UnitViewComp view)
        {
            _view = view;
        }

        protected override void OnTick(float dt)
        {
            if (_remainingSeconds <= 0f)
                return;

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - dt);
            if (_remainingSeconds > FadeDurationSeconds)
                return;

            if (_remainingSeconds <= 0f)
            {
                ClearFlashOverride();
                return;
            }

            SetFlashAmount(_remainingSeconds / FadeDurationSeconds);
        }

        public void Play()
        {
            _remainingSeconds = TotalFlashDurationSeconds;
            SetFlashAmount(1f);
        }

        protected override void OnStop()
        {
            _remainingSeconds = 0f;
            ClearFlashOverride();
        }

        protected override void OnDispose()
        {
            ClearFlashOverride();
        }

        private void SetFlashAmount(float amount)
        {
            SpriteRenderer spriteRenderer = _view.SpriteRenderer;
            if (spriteRenderer == null)
                return;

            spriteRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(FlashAmountId, amount);
            spriteRenderer.SetPropertyBlock(_propertyBlock);
            _hasFlashOverride = true;
        }

        private void ClearFlashOverride()
        {
            if (!_hasFlashOverride)
                return;

            SpriteRenderer spriteRenderer = _view.SpriteRenderer;
            if (spriteRenderer != null)
            {
                spriteRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(FlashAmountId, 0f);
                spriteRenderer.SetPropertyBlock(_propertyBlock);
            }

            _propertyBlock.Clear();
            _hasFlashOverride = false;
        }
    }
}
