using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Authoring;
using Contracts;
using Data.Templates;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class SignaturePresetRepository : IService, ISignaturePresetRepository, IInitialize {
        private ISignaturePresetCompiler _compiler;
        private readonly Dictionary<SignaturePresetDefinition, CompiledSignaturePreset> _cache =
            new(ReferenceIdentityComparer.Instance);
        private bool _initialized;

        Awaitable IInitialize.InitializeAsync(IServiceScope scope) {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            ISignaturePresetCompiler compiler = scope.Get<ISignaturePresetCompiler>();
            _compiler = compiler;
            _initialized = true;

            var source = new AwaitableCompletionSource();
            Awaitable awaitable = source.Awaitable;
            source.SetResult();
            return awaitable;
        }

        public CompiledSignaturePreset GetOrCompile(SignaturePresetDefinition preset) {
            if (!_initialized)
                throw new InvalidOperationException("SignaturePresetRepository must be initialized before use.");
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (_cache.TryGetValue(preset, out CompiledSignaturePreset compiled)) return compiled;
            compiled = _compiler.Compile(preset);
            _cache.Add(preset, compiled);
            return compiled;
        }

        public void Invalidate(SignaturePresetDefinition preset) {
            if (preset == null) return;
            _cache.Remove(preset);
        }

        public void InvalidateById(string presetId) {
            var removed = new List<SignaturePresetDefinition>();
            foreach (SignaturePresetDefinition preset in _cache.Keys)
                if (string.Equals(preset.Id, presetId, StringComparison.Ordinal)) removed.Add(preset);
            foreach (SignaturePresetDefinition preset in removed) _cache.Remove(preset);
        }

        public void Clear() => _cache.Clear();
        public void Dispose() => Clear();

        private sealed class ReferenceIdentityComparer : IEqualityComparer<SignaturePresetDefinition> {
            public static readonly ReferenceIdentityComparer Instance = new();
            public bool Equals(SignaturePresetDefinition x, SignaturePresetDefinition y) => ReferenceEquals(x, y);
            public int GetHashCode(SignaturePresetDefinition obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
