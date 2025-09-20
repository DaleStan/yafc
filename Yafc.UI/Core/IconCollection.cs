﻿using System;
using System.Collections.Generic;
using SDL2;

namespace Yafc.UI;

public static class IconCollection {
    public const int IconSize = 32;
    internal static SDL.SDL_Rect iconRect = new SDL.SDL_Rect { w = IconSize, h = IconSize };

    private static readonly List<(IntPtr, List<(string, SDL.SDL_Rect, SDL.SDL_Rect)>)> icons = [];

    static IconCollection() {
        icons.Add((IntPtr.Zero, []));
        var iconId = Icon.None + 1;

        while (iconId != Icon.FirstCustom) {
            nint surface = SDL_image.IMG_Load("Data/Icons/" + iconId + ".png");
            nint surfaceRgba = SDL.SDL_CreateRGBSurfaceWithFormat(0, IconSize, IconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
            _ = SDL.SDL_FillRect(surfaceRgba, IntPtr.Zero, 0xFFFFFF00);
            _ = SDL.SDL_BlitSurface(surface, IntPtr.Zero, surfaceRgba, IntPtr.Zero);
            SDL.SDL_FreeSurface(surface);
            icons.Add((surfaceRgba, []));
            iconId++;
        }
    }

    public static int IconCount => icons.Count;

    public static Icon AddIcon(IntPtr surface, List<(string, SDL.SDL_Rect src, SDL.SDL_Rect target)> rects) {
        Icon id = (Icon)icons.Count;
        ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface);

        if (surfaceData.w == IconSize && surfaceData.h == IconSize) {
            icons.Add((surface, rects));
        }
        else {
            nint blit = SDL.SDL_CreateRGBSurfaceWithFormat(0, IconSize, IconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
            SDL.SDL_Rect srcRect = new SDL.SDL_Rect { w = surfaceData.w, h = surfaceData.h };
            _ = SDL.SDL_LowerBlitScaled(surface, ref srcRect, blit, ref iconRect);
            icons.Add((blit, rects));
            SDL.SDL_FreeSurface(surface);
        }

        return id;
    }

    public static IntPtr GetIconSurface(Icon icon) => icons[(int)icon].Item1;

    public static void ClearCustomIcons() {
        int firstCustomIconId = (int)Icon.FirstCustom;

        for (int i = firstCustomIconId; i < icons.Count; i++) {
            SDL.SDL_FreeSurface(icons[i].Item1);
        }

        icons.RemoveRange(firstCustomIconId, icons.Count - firstCustomIconId);
    }
}
