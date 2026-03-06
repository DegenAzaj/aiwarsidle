using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Services;
using UnityEngine;

namespace AIWarsIdle.UI.Generators
{
    public sealed class GeneratorsScreenController : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private GeneratorsScreenContext _context;

        [Header("Children")]
        [SerializeField] private GeneratorRowController[] _rows;
        [SerializeField] private HudController _hud;

        [Header("Refresh")]
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        private readonly List<IDisposable> _subscriptions = new();
        private float _carry;

        private void Awake()
        {
            if (_context == null) _context = GetComponentInParent<GeneratorsScreenContext>();
        }

        private void OnEnable()
        {
            _carry = 0;
            SubscribeToEvents();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (_refreshIntervalSeconds <= 0f) return;
            _carry += Time.unscaledDeltaTime;
            if (_carry < _refreshIntervalSeconds) return;
            _carry = 0;
            RefreshAll();
        }

        private void SubscribeToEvents()
        {
            UnsubscribeFromEvents();

            var loop = _context != null ? _context.Loop : null;
            if (loop == null) return;
            if (loop.EventBus == null) return;

            _subscriptions.Add(loop.EventBus.Subscribe<CurrencyChangedEvent>(_ => RefreshAll()));
            _subscriptions.Add(loop.EventBus.Subscribe<GeneratorUpgradedEvent>(_ => RefreshAll()));
            _subscriptions.Add(loop.EventBus.Subscribe<OverclockActivatedEvent>(_ => RefreshAll()));
            _subscriptions.Add(loop.EventBus.Subscribe<OverclockChargeSpentEvent>(_ => RefreshAll()));
            _subscriptions.Add(loop.EventBus.Subscribe<OverclockChargeGainedEvent>(_ => RefreshAll()));
            _subscriptions.Add(loop.EventBus.Subscribe<PrestigeExecutedEvent>(_ => RefreshAll()));
        }

        private void UnsubscribeFromEvents()
        {
            for (var i = 0; i < _subscriptions.Count; i++)
            {
                try { _subscriptions[i]?.Dispose(); }
                catch { /* ignore */ }
            }
            _subscriptions.Clear();
        }

        public void RefreshAll()
        {
            if (_hud != null) _hud.Refresh();

            if (_rows == null) return;
            for (var i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                if (row == null) continue;
                row.Refresh();
            }
        }
    }
}

