import type { Locator } from "@playwright/test";
import { expect } from "./layout-expect";

export async function expectHorizontalButtonGroup(buttons: Locator[]) {
  for (let index = 0; index < buttons.length - 1; index++) {
    const current = buttons[index];
    const next = buttons[index + 1];

    await expect(current).toBeLeftOf(next, { gap: [8, 24] });
    await expect(current).toAlignCenterYWith(next, 4);
    await expect(current).toHaveEqualWidth(next, 32);
  }
}

export async function expectVerticalStack(
  sections: Locator[],
  gap: [number, number] = [12, 32],
) {
  for (let index = 0; index < sections.length - 1; index++) {
    await expect(sections[index]).toBeAbove(sections[index + 1], { gap });
  }
}
