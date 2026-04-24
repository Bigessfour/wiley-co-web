# App Runner Successor Review

This document records the current hosting decision for Wiley Widget after AWS closed App Runner to new customers.

## Decision Summary

- Current platform: AWS App Runner remains the active production host for the thin API.
- Near-term decision: keep App Runner in place for the current production hardening window.
- Recommended successor: ECS Fargate.
- Decision status: approved as the default migration direction unless a later requirement materially changes the API shape.

## Why A Successor Is Required

- AWS App Runner is closed to new customers after April 30, 2026.
- Existing customers can continue operating, but the platform is no longer the right long-term default for a municipal production system that needs durable operations.
- Wiley Widget now depends on production API hosting for Aurora access, Jarvis orchestration, import operations, and council-facing validation.

The project therefore needs an explicit successor plan rather than treating App Runner as permanent.

## Current Hosting Requirements

Any successor platform must preserve these behaviors:

- private network reachability to Aurora PostgreSQL
- stable public HTTPS ingress for the thin API
- IAM-based access to Secrets Manager and tracing/logging services
- controlled rollout and rollback for container images
- predictable warm capacity for council-facing sessions and import workflows
- support for the current ASP.NET Core container without a function-only refactor

## Options Reviewed

### 1. Keep App Runner Indefinitely

Pros:

- lowest immediate migration cost
- current deployment path already works
- existing health checks and logs are already understood

Cons:

- not the recommended long-term posture after service closure to new customers
- limited strategic flexibility compared with ECS
- creates future operational debt by delaying the inevitable hosting review

Decision:

- acceptable as a temporary production host
- rejected as the long-term target

### 2. ECS Fargate

Pros:

- best fit for the existing containerized ASP.NET Core API
- strong VPC, IAM, logging, and scaling control
- clear long-term AWS path for durable container hosting
- easier to evolve toward more advanced operational controls if Wiley Widget grows

Cons:

- higher operational surface area than App Runner
- requires explicit service, task-definition, load-balancer, and deployment setup

Decision:

- recommended successor platform

### 3. Lambda

Pros:

- low idle cost for bursty workloads
- strong managed scaling story for function-oriented APIs

Cons:

- poor fit for the current API shape without intentional refactoring
- cold-start and connection-management tradeoffs are not attractive for the current Aurora and Jarvis patterns
- would add migration complexity while also changing the application hosting model

Decision:

- not recommended for the current product stage

## Migration Triggers

Start the ECS migration project when any of the following becomes true:

- a production release requires hosting capabilities App Runner does not support cleanly
- reliability work begins to center on platform limits rather than application logic
- operator confidence depends on stronger deployment, networking, or rollback control
- a future AWS policy or support change increases App Runner risk materially

## Minimum ECS Migration Scope

The migration project should deliver:

1. ECS Fargate service for the API in the current AWS region and VPC footprint.
2. Public HTTPS endpoint with health checks and controlled deployment behavior.
3. Secrets Manager and IAM parity with the current App Runner runtime.
4. CloudWatch logging, alarm continuity, and release-record updates.
5. Smoke validation for `/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, and `/api/ai/chat`.
6. Rollback procedure documented before cutover.

## Review Cadence

- Review this decision quarterly.
- Re-open the platform choice only if a new hard requirement invalidates ECS Fargate as the best fit.
- Record any hosting decision change in the production handbook and release documentation set.

## Current Conclusion

Wiley Widget does not need an emergency hosting migration today.

It does need a clear default direction.

That direction is:

- keep App Runner as the current production runtime
- treat ECS Fargate as the successor platform
- avoid spending further release time on speculative alternatives unless the API architecture changes substantially
