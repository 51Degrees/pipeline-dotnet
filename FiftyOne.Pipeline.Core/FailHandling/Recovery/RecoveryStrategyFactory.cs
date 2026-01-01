/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

namespace FiftyOne.Pipeline.Core.FailHandling.Recovery
{
    /// <summary>
    /// Factory for creating recovery strategy instances.
    /// </summary>
    public static class RecoveryStrategyFactory
    {
        /// <summary>
        /// Create an exponential backoff recovery strategy with the specified parameters.
        /// </summary>
        /// <param name="initialDelaySeconds">Initial delay in seconds</param>
        /// <param name="maxDelaySeconds">Maximum delay in seconds</param>
        /// <param name="multiplier">Exponential multiplier</param>
        /// <returns>A new ExponentialBackoffRecoveryStrategy instance</returns>
        public static IRecoveryStrategy CreateExponentialBackoff(
            double initialDelaySeconds,
            double maxDelaySeconds,
            double multiplier)
        {
            return new ExponentialBackoffRecoveryStrategy(initialDelaySeconds, maxDelaySeconds, multiplier);
        }

        /// <summary>
        /// Create a simple recovery strategy with the specified delay.
        /// </summary>
        /// <param name="recoverySeconds">Recovery delay in seconds</param>
        /// <returns>A new SimpleRecoveryStrategy instance</returns>
        public static IRecoveryStrategy CreateSimple(double recoverySeconds)
        {
            return new SimpleRecoveryStrategy(recoverySeconds);
        }

        /// <summary>
        /// Create an instant recovery strategy (no delay).
        /// </summary>
        /// <returns>A new InstantRecoveryStrategy instance</returns>
        public static IRecoveryStrategy CreateInstant()
        {
            return new InstantRecoveryStrategy();
        }

        /// <summary>
        /// Create the appropriate recovery strategy based on configuration parameters.
        /// </summary>
        /// <param name="useExponentialBackoff">Whether to use exponential backoff</param>
        /// <param name="recoverySeconds">Simple recovery delay (used if exponential backoff is false)</param>
        /// <param name="initialDelaySeconds">Initial delay for exponential backoff</param>
        /// <param name="maxDelaySeconds">Maximum delay for exponential backoff</param>
        /// <param name="multiplier">Multiplier for exponential backoff</param>
        /// <returns>The appropriate recovery strategy instance</returns>
        public static IRecoveryStrategy Create(
            bool useExponentialBackoff,
            double recoverySeconds,
            double initialDelaySeconds,
            double maxDelaySeconds,
            double multiplier)
        {
            if (useExponentialBackoff)
            {
                return CreateExponentialBackoff(initialDelaySeconds, maxDelaySeconds, multiplier);
            }
            else if (recoverySeconds > 0)
            {
                return CreateSimple(recoverySeconds);
            }
            else
            {
                return CreateInstant();
            }
        }
    }
}