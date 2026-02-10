"use client";

import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { Underline } from '@tiptap/extension-underline';
import { Table } from '@tiptap/extension-table';
import { TableRow } from '@tiptap/extension-table-row';
import { TableCell } from '@tiptap/extension-table-cell';
import { TableHeader } from '@tiptap/extension-table-header';
import { Image } from '@tiptap/extension-image';
import { Color } from '@tiptap/extension-color';
import { TextStyle } from '@tiptap/extension-text-style';
import { FontFamily } from '@tiptap/extension-font-family';
import { TaskList } from '@tiptap/extension-task-list';
import { TaskItem } from '@tiptap/extension-task-item';
import { CharacterCount } from '@tiptap/extension-character-count';
import { Mention } from '@tiptap/extension-mention';
import { Box, ToggleButton, ToggleButtonGroup, Paper, IconButton, Divider, Tooltip, Select, MenuItem, Typography } from '@mui/material';
import {
    FormatBold as FormatBoldIcon,
    FormatItalic as FormatItalicIcon,
    FormatUnderlined as FormatUnderlinedIcon,
    FormatListBulleted as FormatListBulletedIcon,
    FormatListNumbered as FormatListNumberedIcon,
    TableChart as TableChartIcon,
    AddPhotoAlternate as ImageIcon,
    FormatColorText as ColorIcon,
    CheckBox as TaskListIcon,
    Delete as DeleteIcon,
    HighlightOff as RemoveTableIcon,
    AddBox as AddBoxIcon,
    TableRows as TableRowsIcon,
    ViewColumn as ViewColumnIcon,
} from '@mui/icons-material';
import { useEffect, useState } from 'react';

interface RichTextEditorProps {
    value: string; // JSON string from Tiptap or plain text
    onChange: (jsonString: string) => void;
}

const RichTextEditor = ({ value, onChange }: RichTextEditorProps) => {
    const editor = useEditor({
        extensions: [
            StarterKit,
            Underline,
            Table.configure({
                resizable: true,
            }),
            TableRow,
            TableHeader,
            TableCell,
            Image,
            TextStyle,
            Color,
            FontFamily,
            TaskList,
            TaskItem.configure({
                nested: true,
            }),
            CharacterCount,
            Mention,
        ],
        content: '',
        immediatelyRender: false,
        onUpdate: ({ editor }) => {
            const json = editor.getJSON();
            onChange(JSON.stringify(json));
        },
        editorProps: {
            attributes: {
                style: 'min-height: 150px; outline: none; padding: 12px; font-family: inherit; font-size: 0.875rem;',
                class: 'tiptap',
            },
        },
    });

    // Sync external value with editor
    useEffect(() => {
        if (!editor) return;

        let contentObj;
        try {
            contentObj = JSON.parse(value);
            if (!contentObj.type || !contentObj.content) {
                contentObj = {
                    type: 'doc',
                    content: [{ type: 'paragraph', content: [{ type: 'text', text: value }] }]
                };
            }
        } catch (e) {
            contentObj = {
                type: 'doc',
                content: [{ type: 'paragraph', content: [{ type: 'text', text: value }] }]
            };
        }

        const currentJson = JSON.stringify(editor.getJSON());
        const nextJson = JSON.stringify(contentObj);

        if (currentJson !== nextJson) {
            editor.commands.setContent(contentObj, { emitUpdate: false });
        }
    }, [editor, value]);

    if (!editor) return null;

    const addImage = () => {
        const url = window.prompt('URL của hình ảnh:');
        if (url) {
            editor.chain().focus().setImage({ src: url }).run();
        }
    };

    return (
        <Paper variant="outlined" sx={{ overflow: 'hidden', border: '1px solid rgba(0, 0, 0, 0.23)', '&:hover': { borderColor: 'rgba(0, 0, 0, 0.87)' } }}>
            <Box sx={{ borderBottom: '1px solid #e0e0e0', p: 0.5, bgcolor: '#f5f5f5', display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                <ToggleButtonGroup size="small">
                    <Tooltip title="Bold">
                        <ToggleButton value="bold" selected={editor.isActive('bold')} onClick={() => editor.chain().focus().toggleBold().run()}>
                            <FormatBoldIcon fontSize="small" />
                        </ToggleButton>
                    </Tooltip>
                    <Tooltip title="Italic">
                        <ToggleButton value="italic" selected={editor.isActive('italic')} onClick={() => editor.chain().focus().toggleItalic().run()}>
                            <FormatItalicIcon fontSize="small" />
                        </ToggleButton>
                    </Tooltip>
                    <Tooltip title="Underline">
                        <ToggleButton value="underline" selected={editor.isActive('underline')} onClick={() => editor.chain().focus().toggleUnderline().run()}>
                            <FormatUnderlinedIcon fontSize="small" />
                        </ToggleButton>
                    </Tooltip>
                </ToggleButtonGroup>

                <Divider orientation="vertical" flexItem sx={{ mx: 0.5 }} />

                <ToggleButtonGroup size="small">
                    <Tooltip title="Bullet List">
                        <ToggleButton value="bulletList" selected={editor.isActive('bulletList')} onClick={() => editor.chain().focus().toggleBulletList().run()}>
                            <FormatListBulletedIcon fontSize="small" />
                        </ToggleButton>
                    </Tooltip>
                    <Tooltip title="Ordered List">
                        <ToggleButton value="orderedList" selected={editor.isActive('orderedList')} onClick={() => editor.chain().focus().toggleOrderedList().run()}>
                            <FormatListNumberedIcon fontSize="small" />
                        </ToggleButton>
                    </Tooltip>
                    <Tooltip title="Task List">
                        <ToggleButton value="taskList" selected={editor.isActive('taskList')} onClick={() => editor.chain().focus().toggleTaskList().run()}>
                            <TaskListIcon fontSize="small" />
                        </ToggleButton>
                    </Tooltip>
                </ToggleButtonGroup>

                <Divider orientation="vertical" flexItem sx={{ mx: 0.5 }} />

                <Select
                    size="small"
                    value={editor.getAttributes('textStyle').fontFamily || 'Arial'}
                    onChange={(e) => editor.chain().focus().setFontFamily(e.target.value).run()}
                    sx={{ height: 30, fontSize: '0.75rem', minWidth: 100 }}
                >
                    <MenuItem value="Arial">Arial</MenuItem>
                    <MenuItem value="Times New Roman">Times New Roman</MenuItem>
                    <MenuItem value="Courier New">Courier New</MenuItem>
                    <MenuItem value="Georgia">Georgia</MenuItem>
                </Select>

                <input
                    type="color"
                    onInput={(event) => editor.chain().focus().setColor((event.target as HTMLInputElement).value).run()}
                    value={editor.getAttributes('textStyle').color || '#000000'}
                    style={{ width: 30, height: 30, border: 'none', padding: 0, background: 'none', cursor: 'pointer' }}
                    title="Text Color"
                />

                <Divider orientation="vertical" flexItem sx={{ mx: 0.5 }} />

                <Tooltip title="Insert Table">
                    <IconButton size="small" onClick={() => editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()}>
                        <TableChartIcon fontSize="small" />
                    </IconButton>
                </Tooltip>

                {editor.isActive('table') && (
                    <Box sx={{ display: 'flex', gap: 0.2 }}>
                        <Tooltip title="Add Row">
                            <IconButton size="small" onClick={() => editor.chain().focus().addRowAfter().run()}>
                                <TableRowsIcon fontSize="small" />
                            </IconButton>
                        </Tooltip>
                        <Tooltip title="Add Column">
                            <IconButton size="small" onClick={() => editor.chain().focus().addColumnAfter().run()}>
                                <ViewColumnIcon fontSize="small" />
                            </IconButton>
                        </Tooltip>
                        <Tooltip title="Delete Table">
                            <IconButton size="small" color="error" onClick={() => editor.chain().focus().deleteTable().run()}>
                                <RemoveTableIcon fontSize="small" />
                            </IconButton>
                        </Tooltip>
                    </Box>
                )}

                <Tooltip title="Insert Image">
                    <IconButton size="small" onClick={addImage}>
                        <ImageIcon fontSize="small" />
                    </IconButton>
                </Tooltip>
            </Box>

            <Box sx={{ padding: '0 4px' }}>
                <EditorContent editor={editor} />
            </Box>

            <Box sx={{ p: 0.5, borderTop: '1px solid #e0e0e0', bgcolor: '#fafafa', display: 'flex', justifyContent: 'flex-end' }}>
                <Typography variant="caption" color="textSecondary">
                    {editor.storage.characterCount.characters()} characters
                </Typography>
            </Box>

            <style jsx global>{`
                .tiptap p { margin: 0; }
                .tiptap ul, .tiptap ol { margin: 0; padding-left: 20px; }
                .tiptap li { margin: 0; }
                .tiptap table {
                    border-collapse: collapse;
                    table-layout: fixed;
                    width: 100%;
                    margin: 0;
                    overflow: hidden;
                }
                .tiptap table td, .tiptap table th {
                    min-width: 1em;
                    border: 2px solid #ced4da;
                    padding: 3px 5px;
                    vertical-align: top;
                    box-sizing: border-box;
                    position: relative;
                }
                .tiptap table th {
                    font-weight: bold;
                    text-align: left;
                    background-color: #f1f3f5;
                }
                .tiptap img {
                    max-width: 100%;
                    height: auto;
                }
                .tiptap ul[data-type="taskList"] {
                    list-style: none;
                    padding: 0;
                }
                .tiptap ul[data-type="taskList"] li {
                    display: flex;
                    align-items: center;
                }
                .tiptap ul[data-type="taskList"] input[type="checkbox"] {
                    margin-right: 0.5rem;
                }
            `}</style>
        </Paper>
    );
};

export default RichTextEditor;
