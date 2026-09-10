/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

// Test classes run beside one another, which is the MSTest default and
// what makes a suite of this size quick. Nothing here shares mutable
// state, because a compiled script is immutable and every test builds its
// own pipeline.
//
// The tests that must not run beside anything else are marked
// [DoNotParallelize] on the method, which is what this repository already
// does at Tests\FiftyOne.Pipeline.Core.Tests\FlowElements\
// ElementTracingTests.cs line 45. The concurrency test and the benchmark
// are the two that need the machine to themselves.
//
// Saying so here rather than leaving the default also answers analyzer
// MSTEST0001, which asks every test assembly to make the choice plainly
// rather than inherit one.
[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]
