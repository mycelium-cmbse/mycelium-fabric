// ------------------------------------------------------------------------------------------------
//  <copyright file="BrokenReferenceRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Annotations;
    using SysML2.NET.Core.POCO.Root.Dependencies;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Exceptions;

    /// <summary>
    /// The rule that finds the relationships that miss an element which the metamodel requires, for example the typing of a
    /// part after its definition has been removed. The required ends are the properties of the relationship that redefine its
    /// source or its target, or subset its related elements, with a lower multiplicity of 1: the general type of a
    /// specialization, the member of a membership... A relationship whose ends are all optional, such as a connector or an
    /// association, is not checked. The end of a relationship that its owner gives, for example the owning namespace of a
    /// membership, is not checked either: a relationship without owner is left to the <see cref="OrphanElementRule"/>.
    /// </summary>
    public class BrokenReferenceRule : BaseRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrokenReferenceRule"/> class.
        /// </summary>
        public BrokenReferenceRule() : base("broken-reference", ValidationSeverity.Error,
            "A relationship misses an element that the metamodel requires, for example the type of a part whose definition has been removed.")
        {
        }

        /// <summary>
        /// Checks that a relationship refers to the elements that the metamodel requires.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the relationship, or <c>null</c> when the element is not a relationship with an owner that misses an element.</returns>
        protected override string FindProblemCore(IElement element, ValidationContext context)
        {
            if (element is not IRelationship relationship || (relationship.OwningRelatedElement == null && relationship.OwningRelationship == null))
            {
                return null;
            }

            const string Cause = "the element it refers to has been removed, or it was never loaded.";

            try
            {
                var missingEnd = GetRequiredEnds(relationship).FirstOrDefault(end => !end.IsSet).Name;

                return missingEnd == null ? null : $"The {relationship.GetType().Name} has no {missingEnd}, which the metamodel requires: {Cause}";
            }
            catch (IncompleteModelException)
            {
                return $"The {relationship.GetType().Name} refers to an element that is not in the model: {Cause}";
            }
        }

        /// <summary>
        /// Gets the ends of a relationship that the metamodel requires, with a lower multiplicity of 1, except the ends that the
        /// owner of the relationship gives. The most specific metaclasses come first, so that an end is named as in the
        /// metamodel, for example the <c>type</c> of a <c>FeatureTyping</c>, which redefines the <c>general</c> type of a
        /// <c>Specialization</c>.
        /// </summary>
        /// <param name="relationship">The <see cref="IRelationship"/>.</param>
        /// <returns>The name of each required end, and whether it holds an element.</returns>
        private static IEnumerable<(string Name, bool IsSet)> GetRequiredEnds(IRelationship relationship)
        {
            return relationship switch
            {
                IOwningMembership owningMembership => [("ownedMemberElement", owningMembership.ownedMemberElement != null)],
                IMembership membership => [("memberElement", membership.MemberElement != null)],
                IMembershipImport membershipImport => [("importedMembership", membershipImport.ImportedMembership != null)],
                INamespaceImport namespaceImport => [("importedNamespace", namespaceImport.ImportedNamespace != null)],
                IFeatureTyping featureTyping => [("type", featureTyping.Type != null), ("typedFeature", featureTyping.TypedFeature != null)],
                IReferenceSubsetting referenceSubsetting => [("referencedFeature", referenceSubsetting.ReferencedFeature != null)],
                ISpecialization specialization => [("general", specialization.General != null), ("specific", specialization.Specific != null)],
                IConjugation conjugation => [("originalType", conjugation.OriginalType != null), ("conjugatedType", conjugation.ConjugatedType != null)],
                IAnnotation annotation => [("annotatedElement", annotation.AnnotatedElement != null)],
                IDependency dependency => [("client", IsSet(dependency.Client)), ("supplier", IsSet(dependency.Supplier))],
                IDisjoining disjoining => [("typeDisjoined", disjoining.TypeDisjoined != null), ("disjoiningType", disjoining.DisjoiningType != null)],
                IDifferencing differencing => [("differencingType", differencing.DifferencingType != null)],
                IIntersecting intersecting => [("intersectingType", intersecting.IntersectingType != null)],
                IUnioning unioning => [("unioningType", unioning.UnioningType != null)],
                IFeatureChaining featureChaining => [("chainingFeature", featureChaining.ChainingFeature != null)],
                IFeatureInverting featureInverting =>
                    [("featureInverted", featureInverting.FeatureInverted != null), ("invertingFeature", featureInverting.InvertingFeature != null)],
                ITypeFeaturing typeFeaturing => [("featureOfType", typeFeaturing.FeatureOfType != null), ("featuringType", typeFeaturing.FeaturingType != null)],
                _ => [],
            };
        }

        /// <summary>
        /// Tells whether an end of multiplicity <c>1..*</c>, such as the clients of a dependency, holds elements.
        /// </summary>
        /// <param name="elements">The elements at the end of the relationship.</param>
        /// <returns><c>true</c> when the end holds at least one element, and no missing one.</returns>
        private static bool IsSet(List<IElement> elements)
        {
            return elements is { Count: > 0 } && !elements.Contains(null);
        }
    }
}
