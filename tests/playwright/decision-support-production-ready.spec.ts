import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

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
                usedFallback: false,
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
          usedFallback: false,
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

    // 1. Open /wiley-workspace/decision-support from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/decision-support");

    await expect(page.locator("#decision-support-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Decision Support",
    );

    const jarvisSurface = page.locator("#jarvis-chat-ui");
    const fallbackNote = page.getByText(
      "This panel uses the same workspace state the rest of the UI reads and can call the server-side Semantic Kernel assistant when the xAI key is configured.",
    );

    if (await jarvisSurface.count()) {
      await expect(page.locator("#jarvis-runtime-status")).toBeVisible();
      await expect(page.locator("#jarvis-question-input")).toBeVisible();
      await expect(page.locator("#jarvis-conversation-history")).toBeVisible();
      await expect(
        page.locator("#jarvis-recommendation-history"),
      ).toBeVisible();
      await expect(page.locator("#jarvis-chat-answer")).toBeVisible();

      await expect(page.locator("#jarvis-runtime-status")).toContainText(
        "Awaiting Jarvis response",
      );
      await expect(page.locator("#jarvis-conversation-history")).toContainText(
        "No prior Jarvis turns yet.",
      );
      await expect(
        page.locator("#jarvis-recommendation-history"),
      ).toContainText("No saved recommendations yet for this workspace scope.");

      // 2. If the assistant surface is present, ask one short question and then reset the thread.
      const question = "What should I know about the current workspace?";
      await page.locator("#jarvis-question-input").fill(question);
      await expect(
        page.getByRole("button", { name: "Ask Jarvis" }),
      ).toBeEnabled();
      await page.getByRole("button", { name: "Ask Jarvis" }).click();

      await expect(page.locator("#jarvis-chat-answer")).toContainText(
        "Focus on the current rate gap, scenario pressure, and customer mix before publishing.",
      );
      await expect(page.locator("#jarvis-conversation-history")).toContainText(
        question,
      );
      await expect(page.locator("#jarvis-conversation-history")).toContainText(
        "Focus on the current rate gap, scenario pressure, and customer mix before publishing.",
      );
      await expect(
        page.locator("#jarvis-recommendation-history"),
      ).toContainText(
        "Loaded 1 saved recommendation for this workspace scope.",
      );
      await expect(page.locator("#jarvis-runtime-status")).toContainText(
        "Live AI available",
      );

      await page.getByRole("button", { name: "Reset Thread" }).click();
      await expect(page.locator("#jarvis-question-input")).toHaveValue("");
      await expect(page.locator("#jarvis-chat-answer")).toContainText(
        "Jarvis thread reset for the current workspace context.",
      );
      await expect(page.locator("#jarvis-conversation-history")).toContainText(
        "No prior Jarvis turns yet.",
      );
      await expect(
        page.locator("#jarvis-recommendation-history"),
      ).toContainText("No saved recommendations yet for this workspace scope.");
    } else {
      // 3. If only fallback guidance is available, verify the fallback copy and actions remain understandable.
      await expect(fallbackNote).toBeVisible();
      await expect(page.locator("#decision-support-panel")).toContainText(
        "server-side Semantic Kernel assistant",
      );
    }
  });
});
