import type { Locator } from "@playwright/test";

export type LayoutBox = {
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
  centerX: number;
  centerY: number;
};

export type LayoutRange = number | [number, number];

export async function getLayoutBox(
  locator: Locator,
  name = "locator",
): Promise<LayoutBox> {
  await locator.waitFor({ state: "visible", timeout: 30_000 });

  const rawBox = await locator.boundingBox();
  if (!rawBox) {
    throw new Error(`Bounding box not found for ${name}.`);
  }

  return {
    left: rawBox.x,
    top: rawBox.y,
    right: rawBox.x + rawBox.width,
    bottom: rawBox.y + rawBox.height,
    width: rawBox.width,
    height: rawBox.height,
    centerX: rawBox.x + rawBox.width / 2,
    centerY: rawBox.y + rawBox.height / 2,
  };
}

export function normalizeLayoutRange(value: LayoutRange = 0): [number, number] {
  return Array.isArray(value) ? value : [value, value];
}
