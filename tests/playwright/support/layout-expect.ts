import { expect as baseExpect } from "@playwright/test";
import type { Locator } from "@playwright/test";
import { getLayoutBox, normalizeLayoutRange } from "./layout-boxes";
import type { LayoutRange } from "./layout-boxes";

type GapOptions = {
  gap?: LayoutRange;
};

type PaddingOptions = {
  padding?: number;
  tolerance?: number;
};

declare module "@playwright/test" {
  interface Matchers<R, T> {
    toBeAbove(target: Locator, options?: GapOptions): Promise<R>;
    toBeLeftOf(target: Locator, options?: GapOptions): Promise<R>;
    toBeInside(container: Locator, options?: PaddingOptions): Promise<R>;
    toAlignCenterYWith(target: Locator, tolerance?: number): Promise<R>;
    toHaveEqualWidth(target: Locator, tolerance?: number): Promise<R>;
  }
}

export const expect = baseExpect.extend({
  async toBeAbove(locator: Locator, target: Locator, options?: GapOptions) {
    const sourceBox = await getLayoutBox(locator, "source");
    const targetBox = await getLayoutBox(target, "target");
    const [minGap, maxGap] = normalizeLayoutRange(options?.gap);
    const actualGap = targetBox.top - sourceBox.bottom;
    const pass = actualGap >= minGap && actualGap <= maxGap;

    return {
      message: () =>
        `Expected source to be above target with gap ${minGap}-${maxGap}px, got ${actualGap.toFixed(2)}px.`,
      pass,
    };
  },

  async toBeLeftOf(locator: Locator, target: Locator, options?: GapOptions) {
    const sourceBox = await getLayoutBox(locator, "source");
    const targetBox = await getLayoutBox(target, "target");
    const [minGap, maxGap] = normalizeLayoutRange(options?.gap);
    const actualGap = targetBox.left - sourceBox.right;
    const pass = actualGap >= minGap && actualGap <= maxGap;

    return {
      message: () =>
        `Expected source to be left of target with gap ${minGap}-${maxGap}px, got ${actualGap.toFixed(2)}px.`,
      pass,
    };
  },

  async toBeInside(
    locator: Locator,
    container: Locator,
    options?: PaddingOptions,
  ) {
    const sourceBox = await getLayoutBox(locator, "source");
    const containerBox = await getLayoutBox(container, "container");
    const padding = options?.padding ?? 0;
    const tolerance = options?.tolerance ?? 1;
    const pass =
      sourceBox.left + tolerance >= containerBox.left + padding &&
      sourceBox.top + tolerance >= containerBox.top + padding &&
      sourceBox.right <= containerBox.right - padding + tolerance &&
      sourceBox.bottom <= containerBox.bottom - padding + tolerance;

    return {
      message: () =>
        `Expected source to be inside container with ${padding}px padding and ${tolerance}px tolerance. Source: ${formatBox(sourceBox)} Container: ${formatBox(containerBox)}.`,
      pass,
    };
  },

  async toAlignCenterYWith(locator: Locator, target: Locator, tolerance = 2) {
    const sourceBox = await getLayoutBox(locator, "source");
    const targetBox = await getLayoutBox(target, "target");
    const difference = Math.abs(sourceBox.centerY - targetBox.centerY);
    const pass = difference <= tolerance;

    return {
      message: () =>
        `Expected vertical center alignment within ${tolerance}px, got ${difference.toFixed(2)}px.`,
      pass,
    };
  },

  async toHaveEqualWidth(locator: Locator, target: Locator, tolerance = 2) {
    const sourceBox = await getLayoutBox(locator, "source");
    const targetBox = await getLayoutBox(target, "target");
    const difference = Math.abs(sourceBox.width - targetBox.width);
    const pass = difference <= tolerance;

    return {
      message: () =>
        `Expected equal widths within ${tolerance}px, got ${difference.toFixed(2)}px. Source width: ${sourceBox.width.toFixed(2)}px Target width: ${targetBox.width.toFixed(2)}px.`,
      pass,
    };
  },
});

function formatBox(box: Awaited<ReturnType<typeof getLayoutBox>>) {
  return `left=${box.left.toFixed(2)}, top=${box.top.toFixed(2)}, right=${box.right.toFixed(2)}, bottom=${box.bottom.toFixed(2)}`;
}
