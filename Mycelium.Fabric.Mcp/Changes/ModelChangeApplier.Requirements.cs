// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeApplier.Requirements.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Requirements;

    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Behaviors;
    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Kernel.FeatureValues;
    using SysML2.NET.Core.DTO.Kernel.Functions;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.Constraints;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Core.Systems.Requirements;

    /// <content>
    /// The changes that make requirements verifiable: <see cref="ChangeKind.SetConstraint"/>, which builds
    /// <c>subject subj { attribute mass; } require constraint { subj.mass * 1.2 &lt;= 150 }</c> in a requirement, and
    /// <see cref="ChangeKind.Satisfy"/>, which builds <c>satisfy massBudget by eosat1;</c> next to it.
    /// </content>
    public partial class ModelChangeApplier
    {
        /// <summary>
        /// The role, in the messages, of the part that satisfies a requirement.
        /// </summary>
        private const string SatisfyingPartRole = "satisfying part";

        /// <summary>
        /// The name of the subject created in a requirement that has none, as in the <c>RequirementCheck</c> of the SysML v2
        /// library (<c>subject subj</c>).
        /// </summary>
        private const string SubjectName = "subj";

        /// <summary>
        /// The operator of a multiplication, which carries the margin of a constraint.
        /// </summary>
        private const string MultiplicationOperator = "*";

        /// <summary>
        /// The operator of a feature chain expression, such as <c>subj.mass</c>.
        /// </summary>
        private const string FeatureChainOperator = ".";

        /// <summary>
        /// Gives a requirement a constraint on an attribute of its subject, which replaces its previous required constraints.
        /// The subject (<c>subj</c>) and its attribute are created when the requirement does not have them yet.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.SetConstraint"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void SetConstraint(ModelChange change)
        {
            var requirement = this.ResolveRequirement(change.Element);

            if (string.IsNullOrWhiteSpace(change.Attribute))
            {
                throw new InvalidChangeException("The attribute is missing.");
            }

            if (!AttributeConstraint.Operators.Contains(change.Operator))
            {
                var supportedOperators = string.Join(", ", AttributeConstraint.Operators.Select(supportedOperator => $"'{supportedOperator}'"));

                throw new InvalidChangeException($"The operator '{change.Operator}' is not supported. Use {supportedOperators}.");
            }

            if (change.Limit == null)
            {
                throw new InvalidChangeException("The limit is missing.");
            }

            var margin = change.Margin ?? 0;

            if (margin < 0)
            {
                throw new InvalidChangeException("The margin must be 0 or greater.");
            }

            if (margin > 0 && change.Operator == "==")
            {
                throw new InvalidChangeException("A margin cannot be applied with '=='. Use '<=' or '>=' instead.");
            }

            var subject = this.GetOrCreateSubject(requirement);
            var attribute = this.GetOrCreateSubjectAttribute(subject, change.Attribute);
            var constraint = AttributeConstraint.WithMargin(subject.DeclaredName, change.Attribute, change.Operator, change.Limit.Value, margin);

            this.RemoveRequiredConstraints(requirement);

            var constraintUsage = this.Add(new ConstraintUsage { IsComposite = true });
            var constraintMembership = new RequirementConstraintMembership { Kind = RequirementConstraintKind.Requirement, Visibility = VisibilityKind.Public };
            this.AddOwnedRelationship(requirement, constraintMembership, constraintUsage);

            var valueOperand = this.MultiplyBy(this.CreateFeatureChain(subject, attribute), constraint.ValueFactor);
            var limitOperand = this.MultiplyBy(this.Add(new LiteralRational { Value = constraint.Limit }), constraint.LimitFactor);
            var comparison = this.CreateOperation(constraint.Operator, valueOperand, limitOperand);

            this.AddOwnedRelationship(constraintUsage, new ResultExpressionMembership { Visibility = VisibilityKind.Public }, comparison);
        }

        /// <summary>
        /// States that a part satisfies a requirement, with a satisfy link next to the requirement: a
        /// <c>SatisfyRequirementUsage</c> that references the requirement and whose subject is bound to the part.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.Satisfy"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void Satisfy(ModelChange change)
        {
            var requirement = this.ResolveRequirement(change.Element);
            var part = this.Resolve(change.SatisfyingPart, SatisfyingPartRole);
            CheckKind(part, part is IPartUsage, SatisfyingPartRole, "a part");

            if (this.IsSatisfiedBy(requirement, part))
            {
                throw new InvalidChangeException($"The {Describe(requirement)} is already satisfied by the {Describe(part)}.");
            }

            var owner = this.GetOwner(requirement) ?? this.GetOrCreateRootNamespace();
            var satisfy = this.Add(new SatisfyRequirementUsage());
            this.AddOwnedRelationship(owner, CreateMembership(owner, satisfy), satisfy);
            this.AddOwnedRelationship(satisfy, new ReferenceSubsetting { ReferencedFeature = requirement.Id });

            var satisfactionSubject = this.Add(new ReferenceUsage { Direction = FeatureDirectionKind.In });
            this.AddOwnedRelationship(satisfy, new SubjectMembership { Visibility = VisibilityKind.Public }, satisfactionSubject);
            this.AddOwnedRelationship(satisfactionSubject, new FeatureValue { Visibility = VisibilityKind.Public }, this.CreateFeatureReference(part));

            this.createdElements.Add(new CreatedElement(null, satisfy.Id, null, satisfy.GetType().Name));
        }

        /// <summary>
        /// Gets the requirement designated by an identifier or a temporary name.
        /// </summary>
        /// <param name="reference">The <c>Id</c> or temporary name of the requirement.</param>
        /// <returns>The designated requirement.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the reference designates no element (see <see cref="Resolve"/>), or an element that is not a
        /// requirement, a satisfy link being no requirement of its own.
        /// </exception>
        private IElement ResolveRequirement(string reference)
        {
            var requirement = this.Resolve(reference, ElementRole);
            CheckKind(requirement, requirement is IRequirementUsage and not ISatisfyRequirementUsage, ElementRole, "a requirement");

            return requirement;
        }

        /// <summary>
        /// Checks that no other requirement, except the ones marked for deletion, has the given <c>ReqId</c>.
        /// </summary>
        /// <param name="reqId">The identifier of the requirement in its specification.</param>
        /// <exception cref="InvalidChangeException">Thrown when another requirement has this identifier.</exception>
        private void CheckReqIdIsFree(string reqId)
        {
            var existingRequirement = this.elementsById.Values
                .OfType<IRequirementUsage>()
                .FirstOrDefault(requirement => requirement.ReqId == reqId && !this.IsMarkedForDeletion(requirement));

            if (existingRequirement != null)
            {
                throw new InvalidChangeException($"The ReqId '{reqId}' is already used by the {Describe(existingRequirement)}, whose identifier is {existingRequirement.Id}.");
            }
        }

        /// <summary>
        /// Gets the subject of a requirement, and creates it, named <c>subj</c>, when the requirement has none.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <returns>The subject of the requirement.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the subject must be created but a member already has its name.</exception>
        private IElement GetOrCreateSubject(IElement requirement)
        {
            var subject = this.GetElements(requirement.OwnedRelationship)
                .OfType<SubjectMembership>()
                .SelectMany(subjectMembership => this.GetElements(subjectMembership.OwnedRelatedElement))
                .FirstOrDefault();

            if (subject != null)
            {
                return subject;
            }

            this.CheckNameIsFree(requirement, SubjectName);

            var newSubject = this.Add(new ReferenceUsage { DeclaredName = SubjectName, Direction = FeatureDirectionKind.In });
            this.AddOwnedRelationship(requirement, new SubjectMembership { Visibility = VisibilityKind.Public }, newSubject);

            return newSubject;
        }

        /// <summary>
        /// Gets the attribute of the subject of a requirement that has the given name, owned by the subject or by one of
        /// its definitions, and creates it in the subject when there is none: the requirement then states that its subject
        /// has this attribute.
        /// </summary>
        /// <param name="subject">The subject of the requirement.</param>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <returns>The attribute of the subject.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the attribute must be created but a member already has its name.</exception>
        private IElement GetOrCreateSubjectAttribute(IElement subject, string attributeName)
        {
            var definitions = this.GetElements(subject.OwnedRelationship)
                .OfType<IFeatureTyping>()
                .Select(typing => this.elementsById.GetValueOrDefault(typing.Type))
                .Where(definition => definition != null);

            var attribute = definitions
                .Prepend(subject)
                .SelectMany(this.GetOwnedMembers)
                .OfType<IAttributeUsage>()
                .FirstOrDefault(candidate => candidate.DeclaredName == attributeName);

            if (attribute != null)
            {
                return attribute;
            }

            this.CheckNameIsFree(subject, attributeName);

            var newAttribute = this.Add(new AttributeUsage { DeclaredName = attributeName });
            this.AddOwnedRelationship(subject, new FeatureMembership { Visibility = VisibilityKind.Public }, newAttribute);

            return newAttribute;
        }

        /// <summary>
        /// Removes the required constraints of a requirement, with everything they own. Its assumed constraints are kept.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        private void RemoveRequiredConstraints(IElement requirement)
        {
            var requiredConstraintMemberships = this.GetElements(requirement.OwnedRelationship)
                .OfType<IRequirementConstraintMembership>()
                .Where(membership => membership.Kind == RequirementConstraintKind.Requirement)
                .ToList();

            foreach (var membership in requiredConstraintMemberships)
            {
                this.RemoveTree(membership);
            }
        }

        /// <summary>
        /// Tells whether a satisfy link of the working copy, not marked for deletion, already states that a part satisfies
        /// a requirement.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="part">The part.</param>
        /// <returns><c>true</c> when the part already satisfies the requirement.</returns>
        private bool IsSatisfiedBy(IElement requirement, IElement part)
        {
            return this.elementsById.Values
                .OfType<ISatisfyRequirementUsage>()
                .Where(satisfy => !this.IsMarkedForDeletion(satisfy))
                .Where(satisfy => this.GetElements(satisfy.OwnedRelationship).OfType<IReferenceSubsetting>().Any(subsetting => subsetting.ReferencedFeature == requirement.Id))
                .SelectMany(this.CollectTree)
                .OfType<IMembership>()
                .Any(membership => membership is not IOwningMembership && membership.MemberElement == part.Id);
        }

        /// <summary>
        /// Creates the expression <c>source.target</c>, for example <c>subj.mass</c>: a <c>FeatureChainExpression</c> whose
        /// argument references the source and whose target feature is the target.
        /// </summary>
        /// <param name="source">The feature at the start of the chain.</param>
        /// <param name="target">The feature of the source at the end of the chain.</param>
        /// <returns>The new expression.</returns>
        private FeatureChainExpression CreateFeatureChain(IElement source, IElement target)
        {
            var sourceReference = this.CreateFeatureReference(source);
            this.AddEmptyResult(sourceReference);

            var chain = this.Add(new FeatureChainExpression { Operator = FeatureChainOperator });
            this.AddArgument(chain, sourceReference);
            this.AddOwnedRelationship(chain, new Membership { MemberElement = target.Id, Visibility = VisibilityKind.Public });

            return chain;
        }

        /// <summary>
        /// Creates an expression that references a feature, for example <c>eosat1</c> in <c>satisfy massBudget by eosat1</c>:
        /// a <c>FeatureReferenceExpression</c> whose membership designates the feature.
        /// </summary>
        /// <param name="feature">The referenced feature.</param>
        /// <returns>The new expression.</returns>
        private FeatureReferenceExpression CreateFeatureReference(IElement feature)
        {
            var reference = this.Add(new FeatureReferenceExpression());
            this.AddOwnedRelationship(reference, new Membership { MemberElement = feature.Id, Visibility = VisibilityKind.Public });

            return reference;
        }

        /// <summary>
        /// Multiplies an expression by a factor, when there is one.
        /// </summary>
        /// <param name="operand">The expression to multiply.</param>
        /// <param name="factor">The factor, or <c>null</c>.</param>
        /// <returns>The expression <c>operand * factor</c>, or the operand itself when there is no factor.</returns>
        private IElement MultiplyBy(IElement operand, double? factor)
        {
            return factor == null ? operand : this.CreateOperation(MultiplicationOperator, operand, this.Add(new LiteralRational { Value = factor.Value }));
        }

        /// <summary>
        /// Creates a binary operation, for example <c>a &lt;= b</c>: an <c>OperatorExpression</c> with two arguments and an
        /// empty result parameter, as the textual notation builds it.
        /// </summary>
        /// <param name="operatorSymbol">The operator, for example <c>&lt;=</c> or <c>*</c>.</param>
        /// <param name="leftOperand">The expression on the left of the operator.</param>
        /// <param name="rightOperand">The expression on the right of the operator.</param>
        /// <returns>The new expression.</returns>
        private OperatorExpression CreateOperation(string operatorSymbol, IElement leftOperand, IElement rightOperand)
        {
            var operation = this.Add(new OperatorExpression { Operator = operatorSymbol });
            this.AddArgument(operation, leftOperand);
            this.AddArgument(operation, rightOperand);
            this.AddEmptyResult(operation);

            return operation;
        }

        /// <summary>
        /// Adds an argument to an expression: an input parameter whose <c>FeatureValue</c> owns the argument expression.
        /// </summary>
        /// <param name="expression">The expression that receives the argument.</param>
        /// <param name="argument">The argument expression.</param>
        private void AddArgument(IElement expression, IElement argument)
        {
            var parameter = this.Add(new Feature { Direction = FeatureDirectionKind.In });
            this.AddOwnedRelationship(expression, new ParameterMembership { Visibility = VisibilityKind.Public }, parameter);
            this.AddOwnedRelationship(parameter, new FeatureValue { Visibility = VisibilityKind.Public }, argument);
        }

        /// <summary>
        /// Adds to an expression the empty result parameter that the textual notation gives to an operation or a reference.
        /// </summary>
        /// <param name="expression">The expression.</param>
        private void AddEmptyResult(IElement expression)
        {
            var result = this.Add(new Feature { Direction = FeatureDirectionKind.Out });
            this.AddOwnedRelationship(expression, new ReturnParameterMembership { Visibility = VisibilityKind.Public }, result);
        }
    }
}
