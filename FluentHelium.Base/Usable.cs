﻿using NullGuard;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;

namespace FluentHelium.Base
{
    public static class UsableExtensions
    {
        public static Action DoNothing { get; } = () => { };

        public static Usable<T> ToUsable<T>(this T resource, IDisposable usageTime) => new Usable<T>(resource, usageTime.Dispose);

        public static Usable<T> Wrap<T>(this Usable<T> resource, IDisposable usageTime) => new Usable<T>(resource.Resource, () =>
        {
            resource.Dispose();
            usageTime.Dispose();
        });

        public static Usable<T> Wrap<T>(this Usable<T> resource, Action dispose) => new Usable<T>(resource.Resource, () =>
        {
            resource.Dispose();
            dispose();
        });

        public static Usable<T> Wrap<T>(this Usable<T> resource, Action<T> init, Action<T> dispose)
        {
            resource.Do(init);
            return resource.Wrap(() => resource.Do(dispose));
        }

        public static Usable<T> ToSelfUsable<T>(this T resource) where T : IDisposable => resource.ToUsable(resource);

        public static Usable<T> ToUsable<T>(this T resource) => resource.ToUsable(DoNothing);

        public static Usable<T> ToUsable<T>(this T resource, Func<T, IDisposable> usageTimeFactory) => resource.ToUsable(usageTimeFactory(resource));

        public static Usable<T> ToUsable<T>(this T resource, Action dispose) => new Usable<T>(resource, dispose);

        public static Usable<T> ToUsable<T>(this T resource, Action<T> dispose) => resource.ToUsable(() => dispose(resource));

        public static Usable<T> ToUsable<T>(this T resource, Action<T> init, Action<T> dispose)
        {
            init(resource);
            return resource.ToUsable(() => dispose(resource));
        }

        public static Usable<T> ToUsable<T>(this T resource, Func<T, Action> disposeFactory) => resource.ToUsable(() => disposeFactory(resource));

        public static Func<Usable<T>> ToRefCount<T>(this Usable<T> source)
        {
            var refCount = new RefCountDisposable(source);
            return () =>
            {
                var disposable = refCount.GetDisposable();
                refCount.Dispose();
                return source.Resource.ToUsable(disposable);
            };
        }

        public static Usable<T> WrapUsable<T>(this IDisposable usageTime, T resource) => resource.ToUsable(usageTime);

        public static Usable<T> WrapUsable<T, TDisposable>(this TDisposable usageTime, Func<TDisposable, T> factory) where TDisposable : IDisposable =>
            factory(usageTime).ToUsable(usageTime);

        public static Usable<T> Select<TSource, T>(this Usable<TSource> source, Func<TSource, T> selector) =>
            selector(source.Resource).ToUsable(source);

        public static Usable<T> SelectMany<TSource, T>(this Usable<TSource> source, Func<TSource, Usable<T>> selector) =>
            selector(source.Resource).Wrap(source);

        public static Usable<T> Wrap<T>(this Usable<T> source, Func<Usable<T>, IDisposable> selector) =>
            source.Wrap(selector(source));

        public static void Using<T>(this Usable<T> usable, Action<T> action)
        {
            using (usable)
                action(usable.Resource);
        }

        public static TResult Using<T, TResult>(this Usable<T> usable, Func<T, TResult> func)
        {
            using (usable)
                return func(usable.Resource);
        }

        public static void Using<T>(this Func<Usable<T>> factory, Action<T> action) => factory().Using(action);

        public static TResult Using<T, TResult>(this Func<Usable<T>> factory, Func<T, TResult> func) => factory().Using(func);

        public static Usable<T> Do<T>(this Usable<T> usable, Action<T> action)
        {
            action(usable.Resource);
            return usable;
        }

        public static TResult Unwrap<T, TResult>(this Usable<T> usable, Func<T, TResult> func) => func(usable.Resource);

        public static Usable<T> Replace<T>([AllowNull]this Usable<T> usable, Usable<T> @new)
        {
            usable?.Dispose();
            return @new;
        }

        public static Usable<IEnumerable<T>> ToAggregatedUsable<T>(this IEnumerable<Usable<T>> source)
        {
            var disposables = source.ToImmutableList();
            return ((IEnumerable<T>)disposables.Select(u => u.Resource).ToImmutableList()).ToUsable(new CompositeDisposable(disposables));
        }
    }

    /// <summary>
    /// Back side of Lazy 
    /// Lazy can be use after first Value property access
    /// Usable can be use before Dispose call
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class Usable<T> : IDisposable
    {
        internal Usable(T resource, Action dispose) => (_dispose, _resource) = (dispose, resource);

        public void Dispose()
        {
            (_dispose ?? throw new ObjectDisposedException("")).Invoke();
            _dispose = null;
            _resource = default;
        }

        internal T Resource => _dispose == null ? throw new ObjectDisposedException("") : _resource;

        public override string ToString() => _dispose != null ? $"Usable{{{_resource}}}" : $"Disposed<{typeof(T).Name}>";

        Action _dispose;
        T _resource;
    }
}
