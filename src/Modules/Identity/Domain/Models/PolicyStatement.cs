using System.Text.Json;
using System.Text.RegularExpressions;

namespace AlphaZero.Modules.Identity.Domain.Models;

public record PolicyStatement
{
    public string Sid { get; init; } = string.Empty;
    /*
        {
          "Version": "2012-10-17",
          "Statement": [
            {
              "Effect": "Allow",
              "Action": [
                "s3:GetObject",
                "s3:ListBucket"
              ],
              "Resource": [
                "az:tenantId:courses/courseId",
                "arn:aws:s3:::my-bucket/*"
              ]
            },
            {
              "Sid": "DenyDelete",
              "Effect": "Deny",
              "Action": "s3:DeleteObject",
              "Resource": "*"
            },
            {
              "Sid": "ConditionalAccess",
              "Effect": "Allow",
              "Action": "ec2:StartInstances",
              "Resource": "*",
              "Condition": { // will be implemented later
                "IpAddress": {
                  "aws:SourceIp": "203.0.113.0/24"
                },
                "Bool": {
                  "aws:SecureTransport": "true"
                }
              }
            }
          ]
        }
*/
    public List<string> Actions { get; init; } = new List<string>();
    public bool Effect { get; init; } // true for Allow or and false for Deny
    public List<string> Resources { get; init; } = new List<string>();
    public JsonElement? Condition { get; init; } // will be implemented later
}

//for managed policies to not take resources with them
public record PolicyTemplateStatement(string Sid, List<string> Actions, bool Effect); //will implement ResourcePatterns later


