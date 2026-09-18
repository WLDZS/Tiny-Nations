using System;
using System.Collections.Generic;

namespace BorFramework
{
    public sealed class BindableProperty<T>
    {
        private T _value;
        private Action<T> _changed;

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value))
                    return;

                _value = value;
                _changed?.Invoke(_value);
            }
        }

        public BindableProperty(T value = default)
        {
            _value = value;
        }

        public void Subscribe(Action<T> listener)
        {
            if (listener == null)
                return;

            _changed -= listener;
            _changed += listener;
            listener(_value);
        }

        public void Unsubscribe(Action<T> listener)
        {
            _changed -= listener;
        }

        public void Clear()
        {
            _changed = null;
        }
    }
}
