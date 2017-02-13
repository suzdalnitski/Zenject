using System;
using System.Collections.Generic;
using ModestTree;
using ModestTree.Util;

#if ZEN_SIGNALS_ADD_UNIRX
using UniRx;
#endif

namespace Zenject
{
    // This is just used for generic constraints
    public interface ISignal<TParam1, TParam2, TParam3, TParam4> : ISignalBase
    {
        void Fire(TParam1 p1, TParam2 p2, TParam3 p3, TParam4 p4);
    }

    public abstract class Signal<TDerived, TParam1, TParam2, TParam3, TParam4> : SignalBase, ISignal<TParam1, TParam2, TParam3, TParam4>
        where TDerived : Signal<TDerived, TParam1, TParam2, TParam3, TParam4>
    {
        readonly List<Action<TParam1, TParam2, TParam3, TParam4>> _listeners = new List<Action<TParam1, TParam2, TParam3, TParam4>>();
#if ZEN_SIGNALS_ADD_UNIRX
        readonly Subject<ValuePair<TParam1, TParam2, TParam3, TParam4>> _stream = new Subject<ValuePair<TParam1, TParam2, TParam3, TParam4>>();
#endif

#if ZEN_SIGNALS_ADD_UNIRX
        public IObservableRx<ValuePair<TParam1, TParam2, TParam3, TParam4>> Stream
        {
            get
            {
                return _stream;
            }
        }
#endif

        public int NumListeners
        {
            get { return _listeners.Count; }
        }

        public void Listen(Action<TParam1, TParam2, TParam3, TParam4> listener)
        {
            Assert.That(!_listeners.Contains(listener),
                () => "Tried to add method '{0}' to signal '{1}' but it has already been added"
                .Fmt(listener.ToDebugString(), this.GetType()));
            _listeners.Add(listener);
        }

        public void Unlisten(Action<TParam1, TParam2, TParam3, TParam4> listener)
        {
            bool success = _listeners.Remove(listener);
            Assert.That(success,
                () => "Tried to remove method '{0}' from signal '{1}' without adding it first"
                .Fmt(listener.ToDebugString(), this.GetType()));
        }

        public static TDerived operator + (Signal<TDerived, TParam1, TParam2, TParam3, TParam4> signal, Action<TParam1, TParam2, TParam3, TParam4> listener)
        {
            signal.Listen(listener);
            return (TDerived)signal;
        }

        public static TDerived operator - (Signal<TDerived, TParam1, TParam2, TParam3, TParam4> signal, Action<TParam1, TParam2, TParam3, TParam4> listener)
        {
            signal.Unlisten(listener);
            return (TDerived)signal;
        }

        public void Fire(TParam1 p1, TParam2 p2, TParam3 p3, TParam4 p4)
        {
#if UNITY_EDITOR
            using (ProfileBlock.Start("Signal '{0}'", this.GetType().Name))
#endif
            {
                var wasHandled = Manager.Trigger(SignalId, new object[] { p1, p2, p3, p4 });

                wasHandled |= !_listeners.IsEmpty();

                // Use ToArray in case they remove in the handler
                foreach (var listener in _listeners.ToArray())
                {
#if UNITY_EDITOR
                    using (ProfileBlock.Start(listener.ToDebugString()))
#endif
                    {
                        listener(p1, p2, p3, p4);
                    }
                }

#if ZEN_SIGNALS_ADD_UNIRX
                wasHandled |= _stream.HasObservers;
#if UNITY_EDITOR
                using (ProfileBlock.Start("UniRx Stream"))
#endif
                {
                    _stream.OnNext(ValuePair.New(p1, p2, p3, p4));
                }
#endif

                if (Settings.RequiresHandler && !wasHandled)
                {
                    throw Assert.CreateException(
                        "Signal '{0}' was fired but no handlers were attached and the signal is marked to require a handler!", SignalId);
                }
            }
        }
    }
}
