using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AiExamBank.Api.Endpoints;

/// <summary>
/// OpenAPI for the question bank (M4-02) and the school-subject administration routes it depends on.
/// The document is generated from the mapped endpoints, so a route that is not mapped cannot be documented.
/// It is served in Development at <c>/openapi/questions.json</c>; the committed copy under
/// <c>Backend/docs/openapi/</c> is regenerated from the running API (see that folder's README).
/// </summary>
internal static class QuestionOpenApi
{
    public const string DocumentName = "questions";
    private const string CookieScheme = "sessionCookie";
    private const string CsrfScheme = "csrfHeader";
    private static readonly string[] IncludedTags = ["Questions", "School subjects"];

    public static IServiceCollection AddQuestionBankOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            options.ShouldInclude = description =>
                description.ActionDescriptor.EndpointMetadata.OfType<ITagsMetadata>()
                    .Any(tags => tags.Tags.Any(tag => IncludedTags.Contains(tag)));
            options.AddDocumentTransformer(DescribeDocument);
            options.AddOperationTransformer(AddExamples);
            options.AddOperationTransformer(DescribeSearchParameters);
            options.AddSchemaTransformer(RefineSchema);
        });

    public static RouteHandlerBuilder ProducesApiProblem(this RouteHandlerBuilder builder, int status) =>
        builder.Produces<ApiProblem>(status, "application/problem+json");

    /// <summary>For statuses whose body is empty (401 and 403 from the cookie handler, bare 400 and 404 results).</summary>
    public static RouteHandlerBuilder ProducesEmptyStatuses(this RouteHandlerBuilder builder, params int[] statuses)
    {
        foreach (var status in statuses)
            builder.Produces(status);
        return builder;
    }

    private static Task DescribeDocument(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info = new OpenApiInfo
        {
            Title = "AI Exam Bank - Question bank API (M4-02)",
            Version = "v1",
            Description =
                "Question bank CRUD with revisions, filtering and paging, plus the routes that assign a subject to a department.\n\n" +
                "**Authentication.** Sign in with `POST /auth/login` (outside this document); the session is an HttpOnly cookie. " +
                "`POST` and `PUT` also need the `X-CSRF-TOKEN` header: call `GET /auth/csrf` after signing in and send the returned `token`.\n\n" +
                "**Scope.** The school always comes from the signed-in account. A Teacher reads and edits only the questions they own, " +
                "and writes only for subjects assigned to their department. Status is decided by the server; a new question or revision is always `DRAFT`.\n\n" +
                "**Errors.** Business errors are Problem Details with a stable `code` and a `traceId`. 401 and 403 from the authentication layer and a request " +
                "rejected for a missing CSRF token carry no body.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[CookieScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = ".AspNetCore.Identity.Application",
            Description = "Session cookie set by POST /auth/login."
        };
        document.Components.SecuritySchemes[CsrfScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-CSRF-TOKEN",
            Description = "Anti-forgery token from GET /auth/csrf. Required on POST and PUT."
        };

        foreach (var (_, item) in document.Paths)
        {
            foreach (var (method, operation) in item.Operations ?? [])
            {
                var requirement = new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(CookieScheme, document)] = [] };
                if (method != HttpMethod.Get)
                    requirement[new OpenApiSecuritySchemeReference(CsrfScheme, document)] = [];
                operation.Security = [requirement];
            }
        }

        return Task.CompletedTask;
    }

    private static Task AddExamples(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        switch (operation.OperationId)
        {
            case "CreateQuestion":
                RequestExample(operation, "A multiple-choice question", QuestionBody(withBaseRevision: false));
                ProblemExample(operation, "400", "Content breaks several rules at once", InvalidQuestionProblem);
                ProblemExample(operation, "403", "Not a Teacher of the subject's department",
                    Problem(403, "You may not do this to this question.", "QUESTION_FORBIDDEN"));
                break;
            case "UpdateQuestion":
                RequestExample(operation, "A new revision based on revision 1", QuestionBody(withBaseRevision: true));
                ProblemExample(operation, "400", "baseRevision is missing",
                    Problem(400, "The revision being edited is required.", "QUESTION_BASE_REVISION_REQUIRED"));
                ProblemExample(operation, "404", "Unknown, or not readable by the caller",
                    Problem(404, "The question was not found.", "QUESTION_NOT_FOUND"));
                ProblemExample(operation, "409", "baseRevision is no longer the latest",
                    Problem(409, "The question is at revision 2, not revision 1.", "QUESTION_CONFLICT"));
                break;
            case "GetQuestion":
                ProblemExample(operation, "404", "Unknown, or not readable by the caller",
                    Problem(404, "The question was not found.", "QUESTION_NOT_FOUND"));
                break;
            case "SearchQuestions":
                ProblemExample(operation, "400", "Unknown difficulty filter",
                    Problem(400, "The difficulty or status filter is not valid.", "QUESTION_FILTER_INVALID"));
                break;
            case "AssignSchoolSubject":
                RequestExample(operation, "Assign the subject to a department",
                    new JsonObject { ["departmentId"] = "6cdee49d-8909-461c-be94-578010a310d1" });
                ProblemExample(operation, "409", "The subject already has questions in its current department",
                    Problem(409, "The subject already has questions in its current department.", "SCHOOL_SUBJECT_HAS_QUESTIONS"));
                break;
        }

        return Task.CompletedTask;
    }

    private static readonly string[] Difficulties = ["EASY", "MEDIUM", "HARD"];
    private static readonly string[] Statuses = ["DRAFT", "REVIEW_REQUIRED", "APPROVED", "REJECTED"];
    private static readonly string[] Origins = ["MANUAL", "IMPORT", "AI_DRAFT", "SEED"];

    private static Task DescribeSearchParameters(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        if (operation.OperationId != "SearchQuestions" || operation.Parameters is null)
            return Task.CompletedTask;

        var descriptions = new Dictionary<string, string>
        {
            ["subjectId"] = "Only questions of this subject.",
            ["topicId"] = "Only questions of this topic.",
            ["difficulty"] = "EASY, MEDIUM or HARD.",
            ["status"] = "Status of the latest revision: DRAFT, REVIEW_REQUIRED, APPROVED or REJECTED.",
            ["q"] = "Text found in the stem, or a question id. At most 200 characters.",
            ["page"] = "1-based page number, at most 10000. Default 1.",
            ["pageSize"] = "Items per page, 1 to 100. Default 20.",
        };
        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            if (parameter.Name is { } name && descriptions.TryGetValue(name, out var description))
                parameter.Description = description;
            if (parameter.Schema is OpenApiSchema schema)
            {
                if (parameter.Name == "difficulty") schema.Enum = ToNodes(Difficulties);
                if (parameter.Name == "status") schema.Enum = ToNodes(Statuses);
            }
        }

        return Task.CompletedTask;
    }

    private static Task RefineSchema(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        // The generator allows an integer to arrive as a string; this API reads JSON numbers only.
        if (schema.Type is { } type && type.HasFlag(JsonSchemaType.Integer) && type.HasFlag(JsonSchemaType.String))
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        var model = context.JsonTypeInfo.Type;
        if (model == typeof(QuestionRequest))
        {
            // Source is optional, and baseRevision is required only on PUT (the operation text says so).
            schema.Required = new HashSet<string>(["stem", "options", "correctOption", "difficulty", "subjectId", "topicId"]);
            SetEnum(schema, "difficulty", Difficulties);
            SetEnum(schema, "correctOption", ["A", "B", "C", "D"]);
            Describe(schema, "options", "Exactly four different options, in the order A to D.");
            Describe(schema, "baseRevision", "PUT only, required: the revision the author opened. 409 if a newer revision exists.");
            Describe(schema, "subjectId", "Cannot change after the question is created.");
        }
        else if (model == typeof(QuestionSummaryResponse))
        {
            SetEnum(schema, "status", Statuses);
            SetEnum(schema, "difficulty", Difficulties);
        }
        else if (model == typeof(QuestionDetailResponse))
        {
            SetEnum(schema, "status", Statuses);
            SetEnum(schema, "origin", Origins);
            SetEnum(schema, "difficulty", Difficulties);
            SetEnum(schema, "correctOption", ["A", "B", "C", "D"]);
        }
        else if (model == typeof(QuestionRevisionResponse))
        {
            SetEnum(schema, "status", Statuses);
            SetEnum(schema, "origin", Origins);
        }

        return Task.CompletedTask;
    }

    private static void SetEnum(OpenApiSchema schema, string property, string[] values)
    {
        if (schema.Properties is not null && schema.Properties.TryGetValue(property, out var value) && value is OpenApiSchema target)
            target.Enum = ToNodes(values);
    }

    private static void Describe(OpenApiSchema schema, string property, string text)
    {
        if (schema.Properties is not null && schema.Properties.TryGetValue(property, out var value) && value is OpenApiSchema target)
            target.Description = text;
    }

    private static List<JsonNode> ToNodes(IEnumerable<string> values) => values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList();

    private static JsonObject QuestionBody(bool withBaseRevision)
    {
        var body = new JsonObject
        {
            ["stem"] = "Hàm số y = x³ − 3x + 2 đồng biến trên khoảng nào?",
            ["options"] = new JsonArray("(−∞; −1) và (1; +∞)", "(−1; 1)", "(−∞; 1)", "(−1; +∞)"),
            ["correctOption"] = "A",
            ["difficulty"] = "EASY",
            ["subjectId"] = "8ab2a612-3558-435f-9e60-ef3f8e24a8a8",
            ["topicId"] = "223f2355-059b-4195-80e0-6056ea6115f6",
            ["source"] = "SGK Toán 12, tr. 12",
        };
        if (withBaseRevision)
            body["baseRevision"] = 1;
        return body;
    }

    private static JsonObject Problem(int status, string title, string code) => new()
    {
        ["type"] = status switch
        {
            400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            _ => "about:blank",
        },
        ["title"] = title,
        ["status"] = status,
        ["code"] = code,
        ["traceId"] = "00-582083f1f1dcf919d020899ee02964e5-055ec6cc78b72da5-00",
    };

    private static JsonObject InvalidQuestionProblem
    {
        get
        {
            var problem = Problem(400, "The question is not valid.", "QUESTION_INVALID");
            problem["errors"] = new JsonArray(
                new JsonObject { ["code"] = "QUESTION_STEM_REQUIRED", ["field"] = "stem", ["message"] = "The question text is required." },
                new JsonObject { ["code"] = "QUESTION_OPTIONS_COUNT_INVALID", ["field"] = "options", ["message"] = "Exactly 4 options are required." },
                new JsonObject { ["code"] = "QUESTION_CORRECT_OPTION_INVALID", ["field"] = "correctOption", ["message"] = "The correct option must be one of A, B, C, D." },
                new JsonObject { ["code"] = "QUESTION_DIFFICULTY_INVALID", ["field"] = "difficulty", ["message"] = "The difficulty must be easy, medium or hard." });
            return problem;
        }
    }

    private static void RequestExample(OpenApiOperation operation, string summary, JsonNode value)
    {
        if (operation.RequestBody?.Content is not { } content || !content.TryGetValue("application/json", out var media))
            return;
        media.Examples = new Dictionary<string, IOpenApiExample>
        {
            ["example"] = new OpenApiExample { Summary = summary, Value = value }
        };
    }

    private static void ProblemExample(OpenApiOperation operation, string status, string summary, JsonNode value)
    {
        if (operation.Responses is null || !operation.Responses.TryGetValue(status, out var response) ||
            response.Content is not { } content || !content.TryGetValue("application/problem+json", out var media))
            return;
        media.Examples = new Dictionary<string, IOpenApiExample>
        {
            ["example"] = new OpenApiExample { Summary = summary, Value = value }
        };
    }
}
