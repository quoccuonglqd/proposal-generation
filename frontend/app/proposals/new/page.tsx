"use client";

import React, { useState, useEffect, Suspense } from 'react';
import {
    Box,
    Stepper,
    Step,
    StepLabel,
    Button,
    Typography,
    Paper,
    Container,
    FormControl,
    InputLabel,
    Select,
    MenuItem,
    TextField,
    Card,
    CardContent,
    CircularProgress
} from '@mui/material';
import { catalogApi, proposalApi, templateApi } from '../../services/api';
import { useRouter, useSearchParams } from 'next/navigation';
import ServiceSelection from '../../components/ServiceSelection';
import SectionEditor from '../../components/SectionEditor';

const steps = ['Basic Info & Region', 'Select Services', 'Generation Settings'];

function ProposalWizardContent() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const editId = searchParams.get('id');

    const [activeStep, setActiveStep] = useState(0);
    const [regions, setRegions] = useState<any[]>([]);
    const [templates, setTemplates] = useState<any[]>([]);
    const [clientName, setClientName] = useState('');
    const [selectedRegion, setSelectedRegion] = useState('');
    const [selectedTemplate, setSelectedTemplate] = useState('');
    const [selectedServices, setSelectedServices] = useState<any[]>([]);
    const [sections, setSections] = useState<any[]>([]);
    const [generationType, setGenerationType] = useState('pptx');
    const [loading, setLoading] = useState(false);
    const [proposalId, setProposalId] = useState<string | null>(null);

    useEffect(() => {
        catalogApi.getRegions().then((res: any) => setRegions(res.data));
        templateApi.list().then((res: any) => {
            const activeTemplates = res.data.filter((t: any) => t.status === 'Active');
            setTemplates(activeTemplates);
        });

        if (editId) {
            setLoading(true);
            proposalApi.getById(editId).then((res: any) => {
                const proposal = res.data;
                setProposalId(proposal.id);
                setClientName(proposal.clientName);
                setSelectedRegion(proposal.regionId);

                const services = proposal.serviceSelections.map((s: any) => ({
                    serviceId: s.serviceId,
                    quantity: s.quantity,
                    localPrice: s.price.local,
                    usdPrice: s.price.usdRef
                }));
                setSelectedServices(services);

                const sectionList = proposal.sections.map((s: any) => ({
                    key: s.key,
                    order: s.order,
                    content: s.content,
                    backgroundType: s.backgroundType,
                    backgroundAssetId: s.backgroundAssetId
                }));
                setSections(sectionList);

                setLoading(false);
            }).catch(err => {
                console.error('Error loading proposal:', err);
                setLoading(false);
            });
        }
    }, [editId]);

    const handleNext = async () => {
        if (activeStep === 0) {
            if (!proposalId) {
                const res = await proposalApi.create({
                    clientName,
                    regionId: selectedRegion,
                    templateId: selectedTemplate || undefined
                });
                const proposal = res.data;
                console.log('[DEBUG] Proposal Created:', proposal);
                setProposalId(proposal.id);
                if (proposal.sections) {
                    console.log('[DEBUG] Setting sections:', proposal.sections);
                    setSections(proposal.sections.map((s: any) => ({
                        key: s.key,
                        order: s.order,
                        content: s.content,
                        backgroundType: s.backgroundType,
                        backgroundAssetId: s.backgroundAssetId
                    })));
                }
            }
        }
        setActiveStep((prev) => prev + 1);
    };

    const handleBack = () => {
        setActiveStep((prev) => prev - 1);
    };

    const handleFinish = async () => {
        setLoading(true);
        try {
            if (proposalId) {
                await proposalApi.updateMetadata(proposalId, { clientName, regionId: selectedRegion });
                await proposalApi.updateServices(proposalId, selectedServices);

            }
            router.push('/proposals');
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    const renderStepContent = () => {
        switch (activeStep) {
            case 0:
                return (
                    <Box>
                        <TextField
                            label="Client Name"
                            fullWidth
                            value={clientName}
                            onChange={(e) => setClientName(e.target.value)}
                            sx={{ mb: 3 }}
                        />
                        <FormControl fullWidth sx={{ mb: 3 }}>
                            <InputLabel>Region</InputLabel>
                            <Select
                                value={selectedRegion}
                                label="Region"
                                onChange={(e) => setSelectedRegion(e.target.value)}
                            >
                                {regions.map((r) => (
                                    <MenuItem key={r.id} value={r.id}>{r.name} ({r.code})</MenuItem>
                                ))}
                            </Select>
                        </FormControl>

                        <FormControl fullWidth>
                            <InputLabel>Template (Optional)</InputLabel>
                            <Select
                                value={selectedTemplate}
                                label="Template (Optional)"
                                onChange={(e) => setSelectedTemplate(e.target.value)}
                            >
                                <MenuItem value=""><em>None (Standard)</em></MenuItem>
                                {templates.map((t) => (
                                    <MenuItem key={t.id} value={t.id}>{t.name} (v{t.version})</MenuItem>
                                ))}
                            </Select>
                        </FormControl>
                    </Box>
                );
            case 1:
                return (
                    <Box>
                        {selectedRegion ? (
                            <ServiceSelection
                                regionId={selectedRegion}
                                selectedItems={selectedServices}
                                onChange={setSelectedServices}
                            />
                        ) : (
                            <Typography color="error">Please select a region first.</Typography>
                        )}
                    </Box>
                );
            case 2:
                return (
                    <Box>
                        <Card>
                            <CardContent>
                                <Typography variant="h6" gutterBottom>Summary</Typography>
                                <Typography><strong>Client:</strong> {clientName}</Typography>
                                <Typography><strong>Region:</strong> {regions.find(r => r.id === selectedRegion)?.name || 'N/A'}</Typography>
                                <Typography><strong>Services:</strong> {selectedServices.length}</Typography>

                                <FormControl fullWidth sx={{ mt: 3 }}>
                                    <InputLabel>Generation Type</InputLabel>
                                    <Select
                                        value={generationType}
                                        label="Generation Type"
                                        onChange={(e) => setGenerationType(e.target.value)}
                                    >
                                        <MenuItem value="pptx">PPTX Only</MenuItem>
                                        <MenuItem value="pdf">PDF Only</MenuItem>
                                        <MenuItem value="both">Both PPTX and PDF</MenuItem>
                                    </Select>
                                </FormControl>
                            </CardContent>
                        </Card>
                    </Box>
                );
            default:
                return null;
        }
    };

    if (loading && !proposalId) {
        return (
            <Container maxWidth="md" sx={{ py: 8, textAlign: 'center' }}>
                <CircularProgress />
                <Typography sx={{ mt: 2 }}>Loading proposal data...</Typography>
            </Container>
        );
    }

    return (
        <Container maxWidth="md" sx={{ py: 8 }}>
            <Paper sx={{ p: 4 }}>
                <Typography variant="h4" gutterBottom>{editId ? 'Edit Proposal' : 'New Proposal'}</Typography>
                <Stepper activeStep={activeStep} sx={{ mb: 4 }}>
                    {steps.map((label) => (
                        <Step key={label}>
                            <StepLabel>{label}</StepLabel>
                        </Step>
                    ))}
                </Stepper>

                <Box sx={{ minHeight: 300 }}>
                    {renderStepContent()}
                </Box>

                <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 4 }}>
                    <Button
                        disabled={activeStep === 0}
                        onClick={handleBack}
                    >
                        Back
                    </Button>
                    <Box>
                        {activeStep === steps.length - 1 ? (
                            <Button variant="contained" onClick={handleFinish} disabled={loading}>
                                {loading ? 'Saving...' : 'Save Proposal'}
                            </Button>
                        ) : (
                            <Button variant="contained" onClick={handleNext} disabled={!clientName || !selectedRegion}>
                                Next
                            </Button>
                        )}
                    </Box>
                </Box>
            </Paper>
        </Container>
    );
}

export default function NewProposalWizard() {
    return (
        <Suspense fallback={
            <Container maxWidth="md" sx={{ py: 8, textAlign: 'center' }}>
                <CircularProgress />
            </Container>
        }>
            <ProposalWizardContent />
        </Suspense>
    );
}
