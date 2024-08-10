using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using Bonsai;
using Bonsai.IO.Ports;

namespace OpenEphys.Commutator
{
    /// <summary>
    /// Turns an Open Ephys commutator using a sequence of angle steps, in units of turns.
    /// </summary>
    [Description("Turns an Open Ephys commutator using a sequence of angle steps, in units of turns.")]
    public class TurnMotor : Sink<double>
    {
        readonly SerialWriteLine serialWriteLine = new();

        /// <summary>
        /// Gets or sets the name of the serial port that the commutator is plugged into.
        /// </summary>
        [TypeConverter("Bonsai.IO.Ports.PortNameConverter, Bonsai.System")]
        [Description("The name of the serial port that the commutator is plugged into.")]
        public string PortName
        {
            get => serialWriteLine.PortName;
            set => serialWriteLine.PortName = value;
        }

        /// <summary>
        /// Turns an Open Ephys commutator using a sequence of angle steps, in units of turns.
        /// </summary>
        /// <param name="source">
        /// The sequence of angle steps, in units of turns, by which to rotate the commutator.
        /// </param>
        /// <returns>
        /// A sequence which is identical to the <paramref name="source"/> sequence, but
        /// where the commutator is instructed to turn by each step value in the sequence.
        /// </returns>
        public override IObservable<double> Process(IObservable<double> source)
        {
            return Observable.Create<double>(observer =>
            {
                // inner observable will format commands to turn the motor
                var commands = Observable.Create<string>(commandObserver =>
                {
                    var valueObserver = Observer.Create<double>(
                        turns =>
                        {
                            // only format turns for valid values
                            if (!double.IsNaN(turns) && !double.IsInfinity(turns))
                            {
                                var command = $"{{turn: {turns}}}";
                                commandObserver.OnNext(command);
                            }

                            // send all values to the output observer regardless
                            observer.OnNext(turns);
                        },
                        commandObserver.OnError,
                        commandObserver.OnCompleted);
                    return source.SubscribeSafe(valueObserver);
                });

                // route termination notifications to the output observer
                return serialWriteLine.Process(commands).SubscribeSafe(Observer.Create<string>(
                    _ => { },
                    observer.OnError,
                    observer.OnCompleted));
            });
        }
    }
}
