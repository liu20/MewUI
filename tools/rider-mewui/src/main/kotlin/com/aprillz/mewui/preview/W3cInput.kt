// Maps AWT input identifiers to the wire convention (W3C KeyboardEvent.code strings, W3C
// button numbers, ctrl/shift/alt/meta modifier bits) consumed by the preview session.
package com.aprillz.mewui.preview

import java.awt.event.InputEvent
import java.awt.event.KeyEvent
import java.awt.event.MouseEvent

object W3cInput {
    /** Wire modifier bits: 1 ctrl, 2 shift, 4 alt, 8 meta. */
    fun modifiers(event: InputEvent): Int =
        (if (event.modifiersEx and InputEvent.CTRL_DOWN_MASK != 0) 1 else 0) or
            (if (event.modifiersEx and InputEvent.SHIFT_DOWN_MASK != 0) 2 else 0) or
            (if (event.modifiersEx and InputEvent.ALT_DOWN_MASK != 0) 4 else 0) or
            (if (event.modifiersEx and InputEvent.META_DOWN_MASK != 0) 8 else 0)

    /** W3C button number: 0 left, 1 middle, 2 right. */
    fun button(event: MouseEvent): Int = when (event.button) {
        MouseEvent.BUTTON2 -> 1
        MouseEvent.BUTTON3 -> 2
        else -> 0
    }

    /** W3C buttons bitmask after the event: 1 left, 2 right, 4 middle. */
    fun buttons(event: MouseEvent): Int =
        (if (event.modifiersEx and InputEvent.BUTTON1_DOWN_MASK != 0) 1 else 0) or
            (if (event.modifiersEx and InputEvent.BUTTON3_DOWN_MASK != 0) 2 else 0) or
            (if (event.modifiersEx and InputEvent.BUTTON2_DOWN_MASK != 0) 4 else 0)

    /** W3C KeyboardEvent.code for an AWT key code; null when unmapped. */
    fun keyCode(keyCode: Int): String? {
        if (keyCode in KeyEvent.VK_A..KeyEvent.VK_Z) {
            return "Key" + ('A' + (keyCode - KeyEvent.VK_A))
        }
        if (keyCode in KeyEvent.VK_0..KeyEvent.VK_9) {
            return "Digit" + ('0' + (keyCode - KeyEvent.VK_0))
        }
        if (keyCode in KeyEvent.VK_NUMPAD0..KeyEvent.VK_NUMPAD9) {
            return "Numpad" + ('0' + (keyCode - KeyEvent.VK_NUMPAD0))
        }
        if (keyCode in KeyEvent.VK_F1..KeyEvent.VK_F12) {
            return "F" + (1 + (keyCode - KeyEvent.VK_F1))
        }

        return when (keyCode) {
            KeyEvent.VK_BACK_SPACE -> "Backspace"
            KeyEvent.VK_TAB -> "Tab"
            KeyEvent.VK_ENTER -> "Enter"
            KeyEvent.VK_ESCAPE -> "Escape"
            KeyEvent.VK_SPACE -> "Space"
            KeyEvent.VK_LEFT -> "ArrowLeft"
            KeyEvent.VK_UP -> "ArrowUp"
            KeyEvent.VK_RIGHT -> "ArrowRight"
            KeyEvent.VK_DOWN -> "ArrowDown"
            KeyEvent.VK_INSERT -> "Insert"
            KeyEvent.VK_DELETE -> "Delete"
            KeyEvent.VK_HOME -> "Home"
            KeyEvent.VK_END -> "End"
            KeyEvent.VK_PAGE_UP -> "PageUp"
            KeyEvent.VK_PAGE_DOWN -> "PageDown"
            KeyEvent.VK_ADD -> "NumpadAdd"
            KeyEvent.VK_SUBTRACT -> "NumpadSubtract"
            KeyEvent.VK_MULTIPLY -> "NumpadMultiply"
            KeyEvent.VK_DIVIDE -> "NumpadDivide"
            KeyEvent.VK_DECIMAL -> "NumpadDecimal"
            else -> null
        }
    }
}
