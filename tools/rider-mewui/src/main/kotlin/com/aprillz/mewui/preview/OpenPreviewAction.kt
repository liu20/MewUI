// Editor context-menu entry: activates the preview tool window for the right-clicked C# file.
// The tool window auto-starts its session and target auto-match follows the active editor.
package com.aprillz.mewui.preview

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.wm.ToolWindowManager

class OpenPreviewAction : AnAction() {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT

    override fun update(event: AnActionEvent) {
        val extension = event.getData(CommonDataKeys.VIRTUAL_FILE)?.extension
        event.presentation.isEnabledAndVisible =
            event.project != null && extension.equals("cs", ignoreCase = true)
    }

    override fun actionPerformed(event: AnActionEvent) {
        val project = event.project ?: return
        ToolWindowManager.getInstance(project).getToolWindow("MewUI Preview")?.activate(null)
    }
}
