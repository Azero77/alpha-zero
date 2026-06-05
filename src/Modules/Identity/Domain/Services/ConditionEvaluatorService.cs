using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AlphaZero.Modules.Identity.Domain.Services;

public class ConditionEvaluatorService(AuthorizationContext context, IConditionRepository conditionRepository, IEnumerable<IOperationEvaluator> operationEvaluator)
{
    public static readonly JsonValueKind[] AllowedKinds = 
    [
        JsonValueKind.String, 
        JsonValueKind.Number, 
        JsonValueKind.True, 
        JsonValueKind.False, 
        JsonValueKind.Array
    ];

    private IOperationEvaluator getOperationEvaluator(Operator op)
    {
        var evaluator = operationEvaluator.FirstOrDefault(e => e.EvaluatedOperator == op);
        if (evaluator == null)
            throw new NotSupportedException($"No evaluator found for operator {op}");
        return evaluator;
    }

    public ErrorOr<Success> Evaluate(IConditionNode? node)
    {
        if (node == null) return Result.Success;

        return node.Type switch
        {
            ConditionType.And => EvaluateAnd((AndNode)node),
            ConditionType.Or => EvaluateOr((OrNode)node),
            ConditionType.Not => EvaluateNot((NotNode)node),
            ConditionType.Statement => EvaluateStatement((ConditionNode)node),
            ConditionType.Reference => EvaluateReference((ConditionReferenceNode)node),
            _ => Error.Unexpected("Condition.UnknownType", $"Unknown condition type: {node.Type}")
        };
    }

    private ErrorOr<Success> EvaluateReference(ConditionReferenceNode node)
    {
        if (string.IsNullOrEmpty(node.ReferenceName))
            return Error.Validation("Condition.ReferenceNameNullOrEmpty", "Reference name is null or empty");

        var condition = conditionRepository.GetNodeByConditionReferenceName(node.ReferenceName).Result;

        if(condition is null)
            return Error.NotFound("Condition.NotFound", $"Condition with reference name '{node.ReferenceName}' not found");

        return Evaluate(condition);
    }

    private ErrorOr<Success> EvaluateAnd(AndNode node)
    {
        foreach (var condition in node.Conditions)
        {
            var result = Evaluate(condition);
            if (result.IsError) return result;
        }
        return Result.Success;
    }

    private ErrorOr<Success> EvaluateOr(OrNode node)
    {
        List<Error> errors = [];
        foreach (var condition in node.Conditions)
        {
            var result = Evaluate(condition);
            if (!result.IsError) return Result.Success;
            errors.AddRange(result.Errors);
        }
        return errors.Count > 0 ? errors : Error.Conflict("Condition.NotMet", "None of the OR conditions were met");
    }

    private ErrorOr<Success> EvaluateNot(NotNode node)
    {
        var result = Evaluate(node.Condition);
        return result.IsError ? Result.Success : Error.Conflict("Condition.NotMet", "NOT condition failed");
    }

    public ErrorOr<Success> EvaluateStatement(ConditionNode condition)
    {
        var property = typeof(AuthorizationContext).GetProperty(condition.Property, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null) 
            return Error.NotFound("Condition.PropertyNotFound", $"Property '{condition.Property}' not found in AuthorizationContext");

        object? propertyValue = property.GetValue(context);
        if (propertyValue == null) 
            return Error.Failure("Condition.PropertyValueNull", $"Property '{condition.Property}' is null");

        // Handle variable references (e.g., "$TenantId")
        if (condition.Value.ValueKind == JsonValueKind.String)
        {
            string? stringValue = condition.Value.GetString();
            if (stringValue != null && stringValue.StartsWith('$'))
            {
                string variableName = stringValue[1..];
                var varProperty = typeof(AuthorizationContext).GetProperty(variableName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (varProperty == null) 
                    return Error.NotFound("Condition.VariableNotFound", $"Variable '{variableName}' not found in AuthorizationContext");

                object? variableValue = varProperty.GetValue(context);
                if (variableValue == null) 
                    return Error.Failure("Condition.VariableValueNull", $"Variable '{variableName}' is null");

                return EvaluateOperator(propertyValue, variableValue, condition.Operator);
            }
        }

        if (!AllowedKinds.Contains(condition.Value.ValueKind))
            return Error.Validation("Condition.InvalidValueKind", $"JsonValueKind {condition.Value.ValueKind} is not supported");

        return EvaluateJsonOperator(propertyValue, condition.Value, condition.Operator);
    }

    private ErrorOr<Success> EvaluateOperator(object left, object right, Operator op)
    {
        bool isMatch = op switch
        {
            Operator.StringEquals => string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase),
            Operator.StringNotEquals => !string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase),
            Operator.NumericEquals => Equals(left, right) || left.ToString() == right.ToString(),
            Operator.NumericNotEquals => !Equals(left, right) && left.ToString() != right.ToString(),
            _ => throw new NotSupportedException($"Operator {op} is not supported for variable comparison.")
        };
        return isMatch ? Result.Success : Error.Conflict("Condition.NotMet");
    }
    private ErrorOr<Success> EvaluateJsonOperator(object left, JsonElement right, Operator op)
    {
        try
        {
            bool isMatch = op switch
            {
                Operator.StringEquals => string.Equals(left.ToString(), right.GetString(), StringComparison.OrdinalIgnoreCase),
                Operator.StringNotEquals => !string.Equals(left.ToString(), right.GetString(), StringComparison.OrdinalIgnoreCase),
                Operator.StringLike => IsLike(left.ToString(), right.GetString()),
                Operator.StringNotLike => !IsLike(left.ToString(), right.GetString()),
                
                Operator.NumericEquals => Convert.ToDecimal(left) == right.GetDecimal(),
                Operator.NumericNotEquals => Convert.ToDecimal(left) != right.GetDecimal(),
                Operator.NumericLessThan => Convert.ToDecimal(left) < right.GetDecimal(),
                Operator.NumericLessThanEquals => Convert.ToDecimal(left) <= right.GetDecimal(),
                Operator.NumericGreaterThan => Convert.ToDecimal(left) > right.GetDecimal(),
                Operator.NumericGreaterThanEquals => Convert.ToDecimal(left) >= right.GetDecimal(),

                Operator.DateEquals => Convert.ToDateTime(left) == right.GetDateTime(),
                Operator.DateNotEquals => Convert.ToDateTime(left) != right.GetDateTime(),
                Operator.DateLessThan => Convert.ToDateTime(left) < right.GetDateTime(),
                Operator.DateLessThanEquals => Convert.ToDateTime(left) <= right.GetDateTime(),
                Operator.DateGreaterThan => Convert.ToDateTime(left) > right.GetDateTime(),
                Operator.DateGreaterThanEquals => Convert.ToDateTime(left) >= right.GetDateTime(),

                Operator.Bool => Convert.ToBoolean(left) == right.GetBoolean(),

                Operator.IsMainDevice => !getOperationEvaluator(Operator.IsMainDevice).Evaluate(left,right,context).Result.IsError,

                Operator.In => right.ValueKind == JsonValueKind.Array && 
                               right.EnumerateArray().Any(item => EvaluateJsonOperator(left, item, Operator.StringEquals).IsError == false),
                
                Operator.NotIn => right.ValueKind == JsonValueKind.Array && 
                                  !right.EnumerateArray().Any(item => EvaluateJsonOperator(left, item, Operator.StringEquals).IsError == false),

                _ => throw new NotSupportedException($"Operator {op} is not supported.")
            };

            return isMatch ? Result.Success : Error.Conflict("Condition.NotMet");
        }
        catch (Exception ex)
        {
            return Error.Failure("Condition.EvaluationError", ex.Message);
        }
    }

    private static bool IsLike(string? text, string? pattern)
    {
        if (text == null || pattern == null) return false;
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }
}



//Some Operators will have the strategy pattern to evaluate the value based on some heavy logic 
public interface IOperationEvaluator
{
    public Operator EvaluatedOperator { get; }
    public Task<ErrorOr<Success>> Evaluate(object left, JsonElement right, AuthorizationContext context);
}

public class IsMainDeviceOperationEvaluator(IHttpContextAccessor httpContextAccessor, IDeviceSignatureVerifier verifier, IDeviceProvider deviceProvider) : IOperationEvaluator
{
    public Operator EvaluatedOperator => Operator.IsMainDevice;
    public async Task<ErrorOr<Success>> Evaluate(object left, JsonElement right, AuthorizationContext context)
    {
        if(httpContextAccessor is null || httpContextAccessor.HttpContext is null)
            return Error.Failure("Condition.HttpContextUnavailable", "HttpContext is not available");
        bool isSameDevice = string.Equals(left?.ToString(), context.UserMainDeviceId, StringComparison.OrdinalIgnoreCase);
        if (!isSameDevice)
            return Error.Forbidden("Condition.IsMainDeviceFailed", "The device is not the user's main device");

        //validating the sameDevice is not enough , we need to validate the signature of the httpContext item to get more confidence that the request is coming from the same device, this is to prevent token theft and replay attacks
        var deviceId = deviceProvider.GetDeviceId();
        var timestamp = httpContextAccessor.HttpContext.Request.Headers["X-Timestamp"].ToString();
        var signature = httpContextAccessor.HttpContext.Request.Headers["X-Signature"].ToString();

        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
        {
            return Error.Forbidden("Condition.MissingHeaders", "Required headers are missing", new Dictionary<string, object>()
            {
                { "RequiredHeaders", new List<string?>()
                    {
                        "X-Device-Id",
                         "X-Timestamp",
                         "X-Signature"
                    }
                }
            });
        }

        var result = await verifier.VerifySignatureAsync(deviceId, timestamp, signature, httpContextAccessor.HttpContext.Request.Path);
        return result;
    }
}