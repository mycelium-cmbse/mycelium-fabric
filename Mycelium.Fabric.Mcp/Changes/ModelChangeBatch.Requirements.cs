// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Requirements.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;
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
    /// The properties that make requirements verifiable: the <c>ReqId</c> and the constraint of a requirement, which builds
    /// <c>subject subj { attribute mass; } require constraint { subj.mass * 1.2 &lt;= 150 }</c> in the requirement, and the
    /// creation of a <c>SatisfyRequirementUsage</c>, which builds <c>satisfy massBudget by eosat1;</c> next to it.
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The role, in the messages, of the requirement that a satisfy link satisfies.
        /// </summary>
        private const string SatisfiedRequirementRole = "satisfied requirement";

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
        /// The number of decimals of a factor computed from a margin, so that it is written as <c>1.15</c> rather than with
        /// the rounding noise of a binary floating-point number.
        /// </summary>
        private const int MarginFactorDecimalCount = 6;

        /// <summary>
        /// The separator of the names of a path to an attribute of the subject, such as <c>camera.mass</c>.
        /// </summary>
        private const char PathSeparator = '.';

        /// <summary>
        /// The comparison operators that a constraint can use.
        /// </summary>
        private static readonly string[] ComparisonOperators = ["<", "<=", ">", ">=", "==", "!="];

        /// <summary>
        /// Tells whether an element is a requirement, a satisfy link being no requirement of its own.
        /// </summary>
        /// <param name="element">The element to check.</param>
        /// <returns><c>true</c> when the element is a requirement.</returns>
        private static bool IsRequirement(IElement element)
        {
            return element is IRequirementUsage and not ISatisfyRequirementUsage;
        }

        /// <summary>
        /// Checks a constraint before it is built.
        /// </summary>
        /// <param name="constraint">The constraint to check.</param>
        /// <exception cref="InvalidChangeException">Thrown when the constraint is incomplete or invalid.</exception>
        private static void CheckConstraint(ConstraintPayload constraint)
        {
            CheckPath(constraint.Attribute, "The attribute of the constraint is missing.");

            if (!ComparisonOperators.Contains(constraint.Operator))
            {
                var supportedOperators = string.Join(", ", ComparisonOperators.Select(supportedOperator => $"'{supportedOperator}'"));

                throw new InvalidChangeException($"The operator '{constraint.Operator}' is not supported. Use {supportedOperators}.");
            }

            if (constraint.Kind is { } kind && !Enum.IsDefined(kind))
            {
                throw new InvalidChangeException("The kind of the constraint must be Requirement or Assumption.");
            }

            if (constraint.Limit == null && constraint.LimitAttribute == null)
            {
                throw new InvalidChangeException("The limit of the constraint is missing: give a limit or a limitAttribute.");
            }

            if (constraint.Limit != null && constraint.LimitAttribute != null)
            {
                throw new InvalidChangeException("Give either a limit or a limitAttribute, not both.");
            }

            if (constraint.LimitAttribute != null)
            {
                CheckPath(constraint.LimitAttribute, "The limit attribute of the constraint is empty.");
            }

            CheckValue(constraint.Limit, constraint.Unit);

            if (constraint.Limit is { Number: null } && constraint.Operator is not ("==" or "!="))
            {
                throw new InvalidChangeException($"A Boolean or text limit can only be compared with '==' or '!=', not with '{constraint.Operator}'.");
            }

            var margin = constraint.Margin ?? 0;

            if (margin < 0)
            {
                throw new InvalidChangeException("The margin must be 0 or greater.");
            }

            if (margin > 0 && constraint.Operator is "==" or "!=")
            {
                throw new InvalidChangeException($"A margin cannot be applied with '{constraint.Operator}'. Use '<=' or '>=' instead.");
            }

            // A margin multiplies a value or a limit, which makes the constraint harder to meet only for a positive quantity
            // measured from a true zero.
            if (margin > 0 && constraint.Limit?.Number <= 0)
            {
                throw new InvalidChangeException("A margin in percent only applies to a positive limit: include the margin in the limit instead.");
            }

            if (margin > 0 && constraint.Unit != null && ParseUnit(constraint.Unit).Offset != 0)
            {
                throw new InvalidChangeException($"A margin in percent does not apply to a unit with an offset, such as {constraint.Unit}: include the margin in the limit instead.");
            }
        }

        /// <summary>
        /// Checks a path to an attribute of the subject, such as <c>mass</c> or <c>camera.mass</c>.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="missingMessage">The message when the path is missing.</param>
        /// <exception cref="InvalidChangeException">Thrown when the path is missing or has an empty name.</exception>
        private static void CheckPath(string path, string missingMessage)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidChangeException(missingMessage);
            }

            if (path.Split(PathSeparator).Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidChangeException($"The path '{path}' has an empty name: write it as 'mass' or 'camera.mass'.");
            }
        }

        /// <summary>
        /// Checks the properties of a payload that only apply to requirements: the <c>ReqId</c> and the constraint, and
        /// refuses the properties that only apply to the creation of a satisfy link.
        /// </summary>
        /// <param name="element">The created or updated element.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <exception cref="InvalidChangeException">Thrown when a property does not apply to the element, or is invalid.</exception>
        private void CheckRequirementProperties(IElement element, ElementPayload payload)
        {
            if (payload.SatisfiedRequirement != null || payload.SatisfyingPart != null)
            {
                throw new InvalidChangeException("The satisfied requirement and the satisfying part only apply to the creation of a SatisfyRequirementUsage.");
            }

            var hasReqId = !string.IsNullOrWhiteSpace(payload.ReqId);

            if (!hasReqId && payload.Constraint == null)
            {
                return;
            }

            if (!IsRequirement(element))
            {
                throw new InvalidChangeException($"Only a requirement has a reqId or a constraint, not the {Describe(element)}.");
            }

            if (hasReqId)
            {
                this.CheckReqIdIsFree(payload.ReqId, element);
            }

            if (payload.Constraint != null)
            {
                CheckConstraint(payload.Constraint);
            }
        }

        /// <summary>
        /// Sets the properties of a requirement given by the payload, once they are checked: its <c>ReqId</c>, which SysML v2
        /// uses as its short name (<c>requirement &lt;'REQ-SYS-001'&gt;</c>), and its constraint.
        /// </summary>
        /// <param name="element">The created or updated element.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the subject or its attribute must be created but a member already has its name.</exception>
        private void SetRequirementProperties(IElement element, ElementPayload payload)
        {
            if (!string.IsNullOrWhiteSpace(payload.ReqId))
            {
                ((IRequirementUsage)element).ReqId = payload.ReqId;
                this.MarkModified(element);
            }

            if (payload.Constraint != null)
            {
                this.SetConstraint(element, payload.Constraint);
            }
        }

        /// <summary>
        /// Creates a satisfy link, which states that a part satisfies a requirement: a <c>SatisfyRequirementUsage</c> that
        /// references the requirement and whose subject is bound to the part. Without owner, it is created next to the
        /// requirement.
        /// </summary>
        /// <param name="satisfy">The new <c>SatisfyRequirementUsage</c>, which has no identifier yet.</param>
        /// <param name="change">The change that creates it, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreateSatisfy(IElement satisfy, ModelChange change)
        {
            var payload = change.Payload;

            if (payload is not { Definition: null, Value: null, Unit: null, Constraint: null } || !string.IsNullOrWhiteSpace(payload.ReqId))
            {
                throw new InvalidChangeException("A SatisfyRequirementUsage takes a satisfied requirement and a satisfying part, and optionally an owner, a name and a text.");
            }

            var requirement = this.ResolveRequirement(payload.SatisfiedRequirement);
            var part = this.Resolve(payload.SatisfyingPart, SatisfyingPartRole);
            CheckKind(part, part is IPartUsage, SatisfyingPartRole, "a part");

            if (this.IsSatisfiedBy(requirement, part))
            {
                throw new InvalidChangeException($"The {Describe(requirement)} is already satisfied by the {Describe(part)}.");
            }

            var owner = string.IsNullOrWhiteSpace(payload.Owner) ? this.GetOwner(requirement) ?? this.GetOrCreateRootNamespace() : this.Resolve(payload.Owner, OwnerRole);
            CheckKind(owner, owner is INamespace, OwnerRole, "a namespace, for example a package, a definition or a usage");

            if (!string.IsNullOrWhiteSpace(payload.Name))
            {
                this.CheckNameIsFree(owner, payload.Name);
                satisfy.DeclaredName = payload.Name;
            }

            this.AddOwnedMember(owner, satisfy, change.Identity);

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddDocumentation(satisfy, payload.Text);
            }

            this.AddOwnedRelationship(satisfy, new ReferenceSubsetting { ReferencedFeature = requirement.Id });

            var satisfactionSubject = this.Add(new ReferenceUsage { Direction = FeatureDirectionKind.In });
            this.AddOwnedRelationship(satisfy, new SubjectMembership { Visibility = VisibilityKind.Public }, satisfactionSubject);
            this.AddOwnedRelationship(satisfactionSubject, new FeatureValue { Visibility = VisibilityKind.Public }, this.CreateFeatureReference(part));
        }

        /// <summary>
        /// Gives a requirement a constraint on an attribute of its subject: a required constraint, or an assumption, which
        /// replaces its previous constraints of the same kind. The subject (<c>subj</c>) and the attributes of the paths are
        /// created when the requirement does not have them yet.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="constraint">The constraint, already checked by <see cref="CheckConstraint"/>.</param>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the subject or an attribute must be created but a member already has its name, or when a text limit
        /// names several enumeration values.
        /// </exception>
        private void SetConstraint(IElement requirement, ConstraintPayload constraint)
        {
            var kind = constraint.Kind ?? RequirementConstraintKind.Requirement;
            var subject = this.GetOrCreateSubject(requirement);
            var valueOperand = this.CreateSubjectPath(subject, constraint.Attribute);

            var limitOperand = constraint.LimitAttribute == null
                ? this.CreateValueExpression(constraint.Limit.Value, constraint.Unit)
                : this.CreateSubjectPath(subject, constraint.LimitAttribute);

            this.RemoveConstraints(requirement, kind);

            var constraintUsage = this.Add(new ConstraintUsage { IsComposite = true });
            var constraintMembership = new RequirementConstraintMembership { Kind = kind, Visibility = VisibilityKind.Public };
            this.AddOwnedRelationship(requirement, constraintMembership, constraintUsage);

            // The margin always makes the constraint harder to meet: it multiplies the value for an upper limit
            // (subj.mass * 1.2 <= 150) and the limit for a lower limit (subj.capacity >= 300 * 1.2).
            double? marginFactor = constraint.Margin > 0 ? Math.Round(1 + constraint.Margin.Value / 100, MarginFactorDecimalCount) : null;
            var isUpperLimit = constraint.Operator is "<" or "<=";

            var comparison = this.CreateOperation(constraint.Operator, this.MultiplyBy(valueOperand, isUpperLimit ? marginFactor : null),
                this.MultiplyBy(limitOperand, isUpperLimit ? null : marginFactor));

            this.AddOwnedRelationship(constraintUsage, new ResultExpressionMembership { Visibility = VisibilityKind.Public }, comparison);
        }

        /// <summary>
        /// Creates the expression of a path to an attribute of the subject, for example <c>subj.camera.mass</c> for
        /// <c>camera.mass</c>. The features of the path are created in the subject when it does not have them yet.
        /// </summary>
        /// <param name="subject">The subject of the requirement.</param>
        /// <param name="path">The path, already checked by <see cref="CheckPath"/>.</param>
        /// <returns>The feature chain expression.</returns>
        /// <exception cref="InvalidChangeException">Thrown when a feature must be created but a member already has its name.</exception>
        private IElement CreateSubjectPath(IElement subject, string path)
        {
            var names = path.Split(PathSeparator).Select(name => name.Trim()).ToList();
            var owner = subject;
            IElement expression = this.CreateReferenceOperand(subject);

            for (var index = 0; index < names.Count; index++)
            {
                var feature = this.GetOrCreateSubjectMember(owner, names[index], index == names.Count - 1);
                expression = this.CreateFeatureChain(expression, feature);
                owner = feature;
            }

            return expression;
        }

        /// <summary>
        /// Gets the requirement that a satisfy link satisfies, designated by an identifier or a temporary name.
        /// </summary>
        /// <param name="reference">The <c>Id</c> or temporary name of the requirement.</param>
        /// <returns>The designated requirement.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the reference designates no element (see <see cref="Resolve"/>), or an element that is not a
        /// requirement, a satisfy link being no requirement of its own.
        /// </exception>
        private IElement ResolveRequirement(string reference)
        {
            var requirement = this.Resolve(reference, SatisfiedRequirementRole);
            CheckKind(requirement, IsRequirement(requirement), SatisfiedRequirementRole, "a requirement");

            return requirement;
        }

        /// <summary>
        /// Checks that no other requirement, except the ones marked for deletion, has the given <c>ReqId</c>.
        /// </summary>
        /// <param name="reqId">The identifier of the requirement in its specification.</param>
        /// <param name="requirement">The requirement that gets the identifier, which may keep its own.</param>
        /// <exception cref="InvalidChangeException">Thrown when another requirement has this identifier.</exception>
        private void CheckReqIdIsFree(string reqId, IElement requirement)
        {
            var existingRequirement = this.elementsById.Values
                .OfType<IRequirementUsage>()
                .FirstOrDefault(candidate => candidate.Id != requirement.Id && candidate.ReqId == reqId && !this.IsMarkedForDeletion(candidate));

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
        /// Gets the feature of the subject of a requirement (or of a feature of the subject) that has the given name, owned
        /// by it or by one of its definitions, and creates it when there is none: the requirement then states that its
        /// subject has this feature. The last name of a path is an attribute (<c>attribute mass;</c>), the others are
        /// references to sub-parts (<c>ref camera;</c>).
        /// </summary>
        /// <param name="owner">The subject, or a feature of the subject.</param>
        /// <param name="name">The name of the feature.</param>
        /// <param name="isAttribute">Whether the feature is an attribute, at the end of the path.</param>
        /// <returns>The feature.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the feature must be created but a member already has its name.</exception>
        private IElement GetOrCreateSubjectMember(IElement owner, string name, bool isAttribute)
        {
            var definitions = this.GetElements(owner.OwnedRelationship)
                .OfType<IFeatureTyping>()
                .Select(typing => this.elementsById.GetValueOrDefault(typing.Type))
                .Where(definition => definition != null);

            var member = definitions
                .Prepend(owner)
                .SelectMany(this.GetOwnedMembers)
                .OfType<IFeature>()
                .FirstOrDefault(candidate => candidate.DeclaredName == name && (!isAttribute || candidate is IAttributeUsage));

            if (member != null)
            {
                return member;
            }

            this.CheckNameIsFree(owner, name);

            IElement newMember = isAttribute ? new AttributeUsage { DeclaredName = name } : new ReferenceUsage { DeclaredName = name };
            this.AddOwnedRelationship(owner, new FeatureMembership { Visibility = VisibilityKind.Public }, this.Add(newMember));

            return newMember;
        }

        /// <summary>
        /// Removes the constraints of a requirement of one kind (required constraints or assumptions), with everything they
        /// own. Its constraints of the other kind are kept.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="kind">The kind of the constraints to remove.</param>
        private void RemoveConstraints(IElement requirement, RequirementConstraintKind kind)
        {
            var constraintMemberships = this.GetElements(requirement.OwnedRelationship)
                .OfType<IRequirementConstraintMembership>()
                .Where(membership => membership.Kind == kind)
                .ToList();

            foreach (var membership in constraintMemberships)
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
        /// argument is the source expression and whose target feature is the target.
        /// </summary>
        /// <param name="source">The expression at the start of the chain, for example a reference to the subject.</param>
        /// <param name="target">The feature at the end of the chain.</param>
        /// <returns>The new expression.</returns>
        private FeatureChainExpression CreateFeatureChain(IElement source, IElement target)
        {
            var chain = this.Add(new FeatureChainExpression { Operator = FeatureChainOperator });
            this.AddArgument(chain, source);
            this.AddOwnedRelationship(chain, new Membership { MemberElement = target.Id, Visibility = VisibilityKind.Public });

            return chain;
        }

        /// <summary>
        /// Creates an expression that references a feature as an operand, for example <c>subj</c> in <c>subj.mass</c>: a
        /// <c>FeatureReferenceExpression</c> with the empty result parameter that the textual notation gives it.
        /// </summary>
        /// <param name="feature">The referenced feature.</param>
        /// <returns>The new expression.</returns>
        private FeatureReferenceExpression CreateReferenceOperand(IElement feature)
        {
            var reference = this.CreateFeatureReference(feature);
            this.AddEmptyResult(reference);

            return reference;
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
