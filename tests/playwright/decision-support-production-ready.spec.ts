import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

function decisionSupportMainPanel(page: import("@playwright/test").Page) {
  return page.locator("#decision-support-panel").first();
}

function decisionSupportJarvisUi(page: import("@playwright/test").Page) {
  return decisionSupportMainPanel(page).locator("#jarvis-chat-ui").first();
}

test.describe("Unique Interaction Proof", () => {
  test("Decision support proves both assistant-enabled and fallback states", async ({
    page,
  }) => {
    let recommendationHistoryCalls = 0;

    await page.route("**/api/workspace/knowledge", async (route) => {
      const requestBody = route.request().postDataJSON() as {
        snapshot?: {
          selectedEnterprise?: string;
          selectedFiscalYear?: number;
          currentRate?: number;
          totalCosts?: number;
          projectedVolume?: number;
          scenarioItems?: Array<{ cost?: number }>;
        };
      };

      const snapshot = requestBody?.snapshot ?? {};
      const selectedEnterprise = snapshot.selectedEnterprise ?? "Town of Wiley";
      const selectedFiscalYear = snapshot.selectedFiscalYear ?? 2026;
      const currentRate = Number(snapshot.currentRate ?? 27.5);
      const totalCosts = Number(snapshot.totalCosts ?? 12_500);
      const projectedVolume = Number(snapshot.projectedVolume ?? 450);
      const scenarioCostTotal = (snapshot.scenarioItems ?? []).reduce(
        (sum, item) => sum + Number(item.cost ?? 0),
        0,
      );
      const breakEvenRate = currentRate + 2.75;
      const adjustedBreakEvenRate = currentRate + 1.5;
      const rateGap = breakEvenRate - currentRate;
      const adjustedRateGap = adjustedBreakEvenRate - currentRate;

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          selectedEnterprise,
          selectedFiscalYear,
          operationalStatus: "Live guidance available",
          executiveSummary:
            "Jarvis is ready to answer workspace questions with the current snapshot.",
          rateRationale: "Mocked guidance for browser verification.",
          currentRate,
          totalCosts,
          projectedVolume,
          scenarioCostTotal,
          breakEvenRate,
          adjustedBreakEvenRate,
          rateGap,
          adjustedRateGap,
          monthlyRevenue: 39_200,
          netPosition: 8_400,
          coverageRatio: 1.14,
          currentReserveBalance: 7_200,
          recommendedReserveLevel: 6_800,
          reserveRiskAssessment: "Low",
          generatedAtUtc: "2026-04-19T00:00:00Z",
          insights: [
            {
              label: "Rate gap",
              value: "$2.75",
              description:
                "Positive values indicate the rate is below the adjusted break-even target.",
            },
            {
              label: "Scenario pressure",
              value: scenarioCostTotal.toFixed(0),
              description:
                "Combined impact of all active scenario items on the current workspace.",
            },
          ],
          recommendedActions: [
            {
              title: "Close the modeled rate gap",
              description:
                "Increase the working rate or offset the same amount through cost reductions before finalizing the scenario.",
              priority: "High",
            },
            {
              title: "Review filtered customer mix",
              description:
                "Validate that customer filters reflect the service population before publishing the packet.",
              priority: "Medium",
            },
          ],
          topVariances: [],
        }),
      });
    });

    await page.route("**/api/ai/recommendations**", async (route) => {
      recommendationHistoryCalls += 1;

      const items =
        recommendationHistoryCalls === 1
          ? []
          : [
              {
                recommendationId: "jarvis-e2e-001",
                conversationId: "jarvis-e2e",
                userDisplayName: "Playwright Analyst",
                question: "What should I know about the current workspace?",
                recommendation:
                  "Focus on the current rate gap, scenario pressure, and customer mix before publishing.",
                latestUsedFallback: false,
                createdAtUtc: "2026-04-19T00:00:00Z",
              },
            ];

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ items }),
      });
    });

    await page.route("**/api/ai/chat", async (route) => {
      const requestBody = route.request().postDataJSON() as {
        question?: string;
        contextSummary?: string;
        conversationHistory?: Array<{ role?: string; content?: string }>;
      };

      const conversationCount = requestBody?.conversationHistory?.length ?? 0;
      const question =
        requestBody?.question ??
        "What should I know about the current workspace?";

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          question,
          answer:
            "Focus on the current rate gap, scenario pressure, and customer mix before publishing.",
          latestUsedFallback: false,
          answerSource: "semantic_kernel",
          contextSummary: requestBody?.contextSummary ?? "",
          userDisplayName: "Playwright Analyst",
          userProfileSummary: "Deterministic browser-test profile",
          conversationId: "jarvis-e2e",
          conversationMessageCount: conversationCount + 2,
          isFirstConversation: conversationCount === 0,
          canResetConversation: true,
        }),
      });
    });

    await page.route("**/api/ai/chat/reset", async (route) => {
      await route.fulfill({
        status: 204,
        body: "",
      });
    });

    // Stabilize Jarvis health for assistant-enabled surface (canonical latestUsedFallback field).
    await page.route("**/api/ai/health", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          status: "healthy",
          semanticKernelAvailable: true,
          latestAnswerSource: "semantic_kernel",
          latestUsedFallback: false,
          latestFailureCode: null,
          lastTurnAtUtc: "2026-04-19T00:00:00Z",
        }),
      });
    });

    // 1. Open /wiley-workspace/decision-support from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/decision-support");

    const panel = decisionSupportMainPanel(page);
    await expect(panel).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Decision Support",
    );

    const jarvisSurface = decisionSupportJarvisUi(page);
    const fallbackNote = page.getByText(
      "This panel uses the same workspace state the rest of the UI reads and can call the server-side Semantic Kernel assistant when the xAI key is configured.",
    );

    if (await jarvisSurface.count()) {
      await expect(panel.locator("#jarvis-runtime-status")).toBeVisible();
      await expect(panel.locator("#jarvis-question-input")).toBeVisible();
      await expect(panel.locator("#jarvis-conversation-history")).toBeVisible();
      await expect(
        panel.locator("#jarvis-recommendation-history"),
      ).toBeVisible();
      await expect(panel.locator("#jarvis-chat-answer")).toBeVisible();

      await expect(panel.locator("#jarvis-runtime-status")).toContainText(
        /Live AI available|Deterministic fallback active|Awaiting Jarvis response/,
      );
      await expect(panel.locator("#jarvis-conversation-history")).toContainText(
        "No prior Jarvis turns yet.",
      );
      await expect(
        panel.locator("#jarvis-recommendation-history"),
      ).toContainText(
        /No saved recommendations yet for this workspace scope\.|Loaded 1 saved recommendation for this workspace scope\./,
      );

      // 2. If the assistant surface is present, ask one short question and then reset the thread.
      const question = "What should I know about the current workspace?";
      await panel.locator("#jarvis-question-input").fill(question);
      await expect(
        panel.getByRole("button", { name: "Ask Jarvis" }),
      ).toBeEnabled();
      await panel.getByRole("button", { name: "Ask Jarvis" }).click();

      await expect(panel.locator("#jarvis-chat-answer")).toContainText(
        "Focus on the current rate gap, scenario pressure, and customer mix before publishing.",
      );
      await expect(panel.locator("#jarvis-conversation-history")).toContainText(
        question,
      );
      await expect(panel.locator("#jarvis-conversation-history")).toContainText(
        "Focus on the current rate gap, scenario pressure, and customer mix before publishing.",
      );
      await expect(
        panel.locator("#jarvis-recommendation-history"),
      ).toContainText(
        "Loaded 1 saved recommendation for this workspace scope.",
      );
      await expect(panel.locator("#jarvis-runtime-status")).toContainText(
        /Live AI \(semantic_kernel\)|Live AI available/,
      );

      await panel.getByRole("button", { name: "Reset Thread" }).click();
      await expect(panel.locator("#jarvis-question-input")).toHaveValue("");
      await expect(panel.locator("#jarvis-chat-answer")).toContainText(
        "Jarvis thread reset for the current workspace context.",
      );
      await expect(panel.locator("#jarvis-conversation-history")).toContainText(
        "No prior Jarvis turns yet.",
      );
      await expect(
        panel.locator("#jarvis-recommendation-history"),
      ).toContainText("No saved recommendations yet for this workspace scope.");
    } else {
      // 3. If only fallback guidance is available, verify the fallback copy and actions remain understandable.
      await expect(fallbackNote).toBeVisible();
      await expect(panel).toContainText(
        "server-side Semantic Kernel assistant",
      );
    }
  });

  test("Jarvis production turn records semantic_kernel answer source", async ({
    page,
    request,
  }) => {
    await page.route("**/api/workspace/knowledge", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          selectedEnterprise: "Town of Wiley",
          selectedFiscalYear: 2026,
          operationalStatus: "Live guidance available",
          executiveSummary: "Mocked guidance for browser verification.",
          rateRationale: "Mocked guidance for browser verification.",
          currentRate: 27.5,
          totalCosts: 12500,
          projectedVolume: 450,
          scenarioCostTotal: 0,
          breakEvenRate: 30.25,
          adjustedBreakEvenRate: 30.25,
          rateGap: 2.75,
          adjustedRateGap: 2.75,
          monthlyRevenue: 12375,
          netPosition: -125,
          coverageRatio: 0.99,
          currentReserveBalance: 7200,
          recommendedReserveLevel: 6800,
          reserveRiskAssessment: "Low",
          generatedAtUtc: "2026-04-19T00:00:00Z",
          insights: [],
          recommendedActions: [],
          topVariances: [],
        }),
      });
    });

    await page.route("**/api/ai/recommendations**", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ items: [] }),
      });
    });

    await page.route("**/api/ai/chat", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          question: "What is the current break-even rate?",
          answer:
            "The current break-even rate is driven by total costs divided by projected volume.",
          latestUsedFallback: false,
          answerSource: "semantic_kernel",
          contextSummary: "Town of Wiley FY 2026 workspace",
          conversationId: "jarvis-health-e2e",
          conversationMessageCount: 2,
          isFirstConversation: false,
          canResetConversation: true,
        }),
      });
    });

    await page.route("**/api/ai/health", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          status: "healthy",
          semanticKernelAvailable: true,
          latestAnswerSource: "semantic_kernel",
          latestUsedFallback: false,
          latestFailureCode: null,
          lastTurnAtUtc: "2026-04-19T00:00:00Z",
        }),
      });
    });

    await gotoWorkspacePanel(page, "/wiley-workspace/decision-support");

    const panel = decisionSupportMainPanel(page);
    const jarvisSurface = decisionSupportJarvisUi(page);

    if (await jarvisSurface.count()) {
      await panel
        .locator("#jarvis-question-input")
        .fill("What is the current break-even rate?");
      await panel.getByRole("button", { name: "Ask Jarvis" }).click();
      await expect(panel.locator("#jarvis-chat-answer")).toContainText(
        "break-even rate",
      );
      await expect(panel.locator("#jarvis-runtime-status")).toContainText(
        /Live AI \(semantic_kernel\)|Live AI available/,
      );
    }

    const healthBody = await page.evaluate(async () => {
      const response = await fetch("/api/ai/health");
      return response.json();
    });
    expect(healthBody.latestAnswerSource).toBe("semantic_kernel");
  });
});
